using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public class StudentParentLinkingService
{
    private readonly AppDbContext _db;

    public StudentParentLinkingService(AppDbContext db)
    {
        _db = db;
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// 當輸入學生手機（最多 3 支）時，比對家長聯絡電話；若相同則自動建立關聯，未命中則維持未綁定。
    /// </summary>
    public async Task<int> AutoLinkStudentByPhonesAsync(int studentId)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return 0;

        var studentPhones = student.AllPhones
            .Select(NormalizePhone)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet();

        if (!studentPhones.Any()) return 0;

        var parents = await _db.Users
            .Where(u => u.Role == UserRole.Parent && !string.IsNullOrEmpty(u.Phone))
            .ToListAsync();

        var existingParentIds = await _db.ParentStudentLinks
            .Where(l => l.StudentId == studentId)
            .Select(l => l.ParentUserId)
            .ToHashSetAsync();

        var addedCount = 0;
        foreach (var parent in parents)
        {
            var pPhone = NormalizePhone(parent.Phone);
            if (!string.IsNullOrEmpty(pPhone) && studentPhones.Contains(pPhone))
            {
                if (!existingParentIds.Contains(parent.Id))
                {
                    _db.ParentStudentLinks.Add(new ParentStudentLink
                    {
                        ParentUserId = parent.Id,
                        StudentId = studentId,
                        Relationship = "家長"
                    });
                    existingParentIds.Add(parent.Id);
                    addedCount++;
                }
            }
        }

        if (addedCount > 0)
        {
            await _db.SaveChangesAsync();
        }

        return addedCount;
    }

    /// <summary>
    /// 當家長電話輸入/更新後，比對所有學生的三個手機欄位；若有相同自動關聯。
    /// </summary>
    public async Task<int> AutoLinkParentByPhoneAsync(int parentUserId)
    {
        var parent = await _db.Users.FindAsync(parentUserId);
        if (parent == null || parent.Role != UserRole.Parent) return 0;

        var parentPhone = NormalizePhone(parent.Phone);
        if (string.IsNullOrEmpty(parentPhone)) return 0;

        var students = await _db.Students.ToListAsync();
        var existingStudentIds = await _db.ParentStudentLinks
            .Where(l => l.ParentUserId == parentUserId)
            .Select(l => l.StudentId)
            .ToHashSetAsync();

        var addedCount = 0;
        foreach (var student in students)
        {
            var studentPhones = student.AllPhones
                .Select(NormalizePhone)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet();

            if (studentPhones.Contains(parentPhone))
            {
                if (!existingStudentIds.Contains(student.Id))
                {
                    _db.ParentStudentLinks.Add(new ParentStudentLink
                    {
                        ParentUserId = parentUserId,
                        StudentId = student.Id,
                        Relationship = "家長"
                    });
                    existingStudentIds.Add(student.Id);
                    addedCount++;
                }
            }
        }

        if (addedCount > 0)
        {
            await _db.SaveChangesAsync();
        }

        return addedCount;
    }
}
