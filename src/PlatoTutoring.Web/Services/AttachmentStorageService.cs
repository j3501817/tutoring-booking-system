using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public class AttachmentStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    };

    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly AppDbContext _db;
    private readonly string _storageRoot;
    private readonly BlobContainerClient? _blobContainerClient;

    public bool IsUsingBlobStorage => _blobContainerClient != null;
    public string StorageModeDescription { get; }

    public AttachmentStorageService(
        AppDbContext db,
        IWebHostEnvironment env,
        IConfiguration? config = null)
    {
        _db = db;
        _storageRoot = Path.Combine(env.ContentRootPath, "App_Data", "PrivateAttachments");
        Directory.CreateDirectory(_storageRoot);

        var containerName = GetEnvOrConfig(config, "BLOB_CONTAINER_NAME", "AzureBlob:ContainerName")
                            ?? "plato-remittance-attachments";

        var blobConnStr = GetEnvOrConfig(config, "BLOB_CONNECTION_STRING", "AzureBlob:ConnectionString");
        var blobAccount = GetEnvOrConfig(config, "BLOB_ACCOUNT_NAME", "AzureBlob:AccountName")
                          ?? GetEnvOrConfig(config, "BLOB_USER", "AzureBlob:User");
        var blobKey = GetEnvOrConfig(config, "BLOB_ACCOUNT_KEY", "AzureBlob:AccountKey")
                      ?? GetEnvOrConfig(config, "BLOB_PASSWORD", "AzureBlob:Password");
        var blobEndpoint = GetEnvOrConfig(config, "BLOB_ENDPOINT_URL", "AzureBlob:EndpointUrl");

        if (!string.IsNullOrWhiteSpace(blobConnStr))
        {
            _blobContainerClient = new BlobContainerClient(blobConnStr, containerName);
            StorageModeDescription = $"Azure Blob Storage (ConnectionString / Container: {containerName})";
        }
        else if (!string.IsNullOrWhiteSpace(blobAccount) && !string.IsNullOrWhiteSpace(blobKey))
        {
            var serviceUri = !string.IsNullOrWhiteSpace(blobEndpoint)
                ? new Uri($"{blobEndpoint.TrimEnd('/')}/{containerName}")
                : new Uri($"https://{blobAccount}.blob.core.windows.net/{containerName}");

            var credential = new StorageSharedKeyCredential(blobAccount, blobKey);
            _blobContainerClient = new BlobContainerClient(serviceUri, credential);
            StorageModeDescription = $"Azure Blob Storage (帳號: {blobAccount} / Container: {containerName})";
        }
        else
        {
            StorageModeDescription = "本機私有儲存目錄 (App_Data/PrivateAttachments)";
        }
    }

    private static string? GetEnvOrConfig(IConfiguration? config, string envName, string configKey)
    {
        var envVal = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(envVal))
            return envVal.Trim();

        var cfgVal = config?[configKey] ?? config?[envName];
        return string.IsNullOrWhiteSpace(cfgVal) ? null : cfgVal.Trim();
    }

    public async Task<RemittanceAttachment> SaveAttachmentAsync(
        string originalFileName,
        string contentType,
        Stream contentStream,
        int uploadedByUserId,
        int retentionDays = 90,
        DateTime? nowUtc = null)
    {
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new InvalidOperationException("不支援的附件檔案格式，僅允許 JPG、PNG、WEBP 或 PDF。");
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("不支援的附件 MIME 類型。");
        }

        if (contentStream.Length <= 0 || contentStream.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("附件檔案大小必須介於 1 Byte 至 10 MB 之間。");
        }

        var timestamp = nowUtc ?? DateTime.UtcNow;
        var storedFileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";

        if (_blobContainerClient != null)
        {
            await _blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            var blobClient = _blobContainerClient.GetBlobClient(storedFileName);
            contentStream.Position = 0;
            await blobClient.UploadAsync(contentStream, new BlobHttpHeaders { ContentType = contentType });
        }
        else
        {
            var fullPath = Path.Combine(_storageRoot, storedFileName);
            await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await contentStream.CopyToAsync(fs);
        }

        var attachment = new RemittanceAttachment
        {
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = storedFileName,
            ContentType = contentType,
            FileSizeBytes = contentStream.Length,
            UploadedByUserId = uploadedByUserId,
            UploadedAtUtc = timestamp,
            ExpiresAtUtc = timestamp.AddDays(retentionDays),
            IsDeleted = false
        };

        _db.RemittanceAttachments.Add(attachment);
        await _db.SaveChangesAsync();
        return attachment;
    }

    public (string FullPath, RemittanceAttachment Metadata)? GetAttachmentFile(RemittanceAttachment attachment)
    {
        if (attachment.IsDeleted) return null;
        var fullPath = Path.Combine(_storageRoot, attachment.StoredFileName);
        if (!File.Exists(fullPath)) return null;
        return (fullPath, attachment);
    }

    public async Task<Stream?> OpenAttachmentStreamAsync(RemittanceAttachment attachment)
    {
        if (attachment.IsDeleted) return null;

        if (_blobContainerClient != null)
        {
            var blobClient = _blobContainerClient.GetBlobClient(attachment.StoredFileName);
            if (await blobClient.ExistsAsync())
            {
                var response = await blobClient.DownloadStreamingAsync();
                return response.Value.Content;
            }
        }

        var fullPath = Path.Combine(_storageRoot, attachment.StoredFileName);
        if (!File.Exists(fullPath)) return null;
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task<int> PurgeExpiredAttachmentsAsync(DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var expiredList = await _db.RemittanceAttachments
            .Where(a => !a.IsDeleted && a.ExpiresAtUtc <= timestamp)
            .ToListAsync();

        foreach (var att in expiredList)
        {
            if (_blobContainerClient != null)
            {
                var blobClient = _blobContainerClient.GetBlobClient(att.StoredFileName);
                await blobClient.DeleteIfExistsAsync();
            }

            var fullPath = Path.Combine(_storageRoot, att.StoredFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            att.IsDeleted = true;
            att.DeletedAtUtc = timestamp;
        }

        await _db.SaveChangesAsync();
        return expiredList.Count;
    }
}
