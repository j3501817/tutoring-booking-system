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

    public AttachmentStorageService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _storageRoot = Path.Combine(env.ContentRootPath, "App_Data", "PrivateAttachments");
        Directory.CreateDirectory(_storageRoot);
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
        var fullPath = Path.Combine(_storageRoot, storedFileName);

        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
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

    public async Task<int> PurgeExpiredAttachmentsAsync(DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var expiredList = await _db.RemittanceAttachments
            .Where(a => !a.IsDeleted && a.ExpiresAtUtc <= timestamp)
            .ToListAsync();

        foreach (var att in expiredList)
        {
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
