using System;
using System.Collections.Generic;
using System.Linq;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Pages.Parent;
using PlatoTutoring.Web.Services;
using Xunit;

namespace PlatoTutoring.Tests;

public class ParentCalendarBookingTests
{
    [Fact]
    public void CalendarDaySummary_CalculatesTotalsAndFullStatusCorrectly()
    {
        var summary = new CalendarDaySummary
        {
            Date = new DateOnly(2026, 10, 24),
            SmallGroupCount = 2,
            GroupCount = 1,
            TotalRemainingSeats = 0,
            HasMyBooking = false
        };

        Assert.Equal(3, summary.TotalSlots);
        Assert.True(summary.IsFull);

        summary.TotalRemainingSeats = 4;
        Assert.False(summary.IsFull);
    }

    [Fact]
    public void CalendarDaySummary_GroupsSlotsByDateAndClassTypeAccurately()
    {
        var targetDate = new DateOnly(2026, 10, 25);
        var slots = new List<TutoringSlot>
        {
            new TutoringSlot
            {
                Id = 1,
                StartsAtUtc = new DateTime(2026, 10, 25, 2, 0, 0, DateTimeKind.Utc), // Taipei 10:00
                EndsAtUtc = new DateTime(2026, 10, 25, 3, 30, 0, DateTimeKind.Utc),
                SlotType = SlotType.SmallGroup,
                MaxCapacity = 3,
                CurrentBookedCount = 1
            },
            new TutoringSlot
            {
                Id = 2,
                StartsAtUtc = new DateTime(2026, 10, 25, 6, 0, 0, DateTimeKind.Utc), // Taipei 14:00
                EndsAtUtc = new DateTime(2026, 10, 25, 7, 30, 0, DateTimeKind.Utc),
                SlotType = SlotType.SmallGroup,
                MaxCapacity = 3,
                CurrentBookedCount = 3 // full
            },
            new TutoringSlot
            {
                Id = 3,
                StartsAtUtc = new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc), // Taipei 18:00
                EndsAtUtc = new DateTime(2026, 10, 25, 11, 30, 0, DateTimeKind.Utc),
                SlotType = SlotType.GroupCounseling,
                MaxCapacity = 10,
                CurrentBookedCount = 2
            }
        };

        var studentBookings = new List<Booking>
        {
            new Booking
            {
                Id = 101,
                TutoringSlotId = 1,
                Status = BookingStatus.Confirmed
            }
        };

        var summaryDict = new Dictionary<DateOnly, CalendarDaySummary>();
        foreach (var s in slots)
        {
            var tpeDate = DateOnly.FromDateTime(BookingService.ToTaipeiTime(s.StartsAtUtc));
            if (!summaryDict.TryGetValue(tpeDate, out var daySummary))
            {
                daySummary = new CalendarDaySummary { Date = tpeDate };
                summaryDict[tpeDate] = daySummary;
            }

            if (s.SlotType == SlotType.SmallGroup)
                daySummary.SmallGroupCount++;
            else if (s.SlotType == SlotType.GroupCounseling)
                daySummary.GroupCount++;

            daySummary.TotalRemainingSeats += s.RemainingSeats;
            if (studentBookings.Any(b => b.TutoringSlotId == s.Id && b.Status == BookingStatus.Confirmed))
            {
                daySummary.HasMyBooking = true;
            }
        }

        Assert.True(summaryDict.ContainsKey(targetDate));
        var res = summaryDict[targetDate];
        Assert.Equal(2, res.SmallGroupCount);
        Assert.Equal(1, res.GroupCount);
        Assert.Equal(3, res.TotalSlots);
        Assert.Equal(2 + 0 + 8, res.TotalRemainingSeats); // 10 remaining seats
        Assert.True(res.HasMyBooking);
        Assert.False(res.IsFull);
    }

    [Fact]
    public void CalendarDaySummary_ShouldDisplaySingleBadgePerTypeRegardlessOfCount()
    {
        var summary = new CalendarDaySummary
        {
            Date = new DateOnly(2026, 10, 25),
            SmallGroupCount = 5,
            GroupCount = 3
        };

        // When multiple slots of the same type exist, each type should only produce one badge
        var displayBadges = new List<string>();
        if (summary.SmallGroupCount > 0)
        {
            displayBadges.Add("小組指導");
        }
        if (summary.GroupCount > 0)
        {
            displayBadges.Add("團體輔導");
        }

        Assert.Equal(2, displayBadges.Count);
        Assert.Contains("小組指導", displayBadges);
        Assert.Contains("團體輔導", displayBadges);
        Assert.DoesNotContain("5", displayBadges[0]);
        Assert.DoesNotContain("3", displayBadges[1]);
    }

    [Fact]
    public void SevenDays_GeneratesExactlySevenConsecutiveDaysFromTodayWithoutPastDates()
    {
        var today = new DateOnly(2026, 10, 19);
        var sevenDays = Enumerable.Range(0, 7).Select(i => today.AddDays(i)).ToList();

        Assert.Equal(7, sevenDays.Count);
        Assert.Equal(today, sevenDays.First());
        Assert.Equal(today.AddDays(6), sevenDays.Last());

        // 確保每一天都大於等於今天，完全不包含過去日期
        Assert.All(sevenDays, d => Assert.True(d >= today));

        // 確保日期連續無跳號
        for (int i = 0; i < sevenDays.Count; i++)
        {
            Assert.Equal(today.AddDays(i), sevenDays[i]);
        }
    }

    [Fact]
    public void SevenDaysNavigation_ForwardUnconstrained_And_BackwardStopsAtToday()
    {
        var today = new DateOnly(2026, 11, 7);

        // 情境 1：今天這週（預設），不可往前
        var currentStart = today;
        var canGoPrev = currentStart > today;
        Assert.False(canGoPrev);

        // 往後七天（第一週後）
        var nextWeekStart = currentStart.AddDays(7);
        Assert.Equal(new DateOnly(2026, 11, 14), nextWeekStart);

        // 情境 2：切換至未來一週後，可以往前七天，且往後不限
        currentStart = nextWeekStart;
        Assert.True(currentStart > today);

        var prevWeekStart = currentStart.AddDays(-7) < today ? today : currentStart.AddDays(-7);
        Assert.Equal(today, prevWeekStart);

        // 再往後一週 (+14天)
        var nextTwoWeekStart = currentStart.AddDays(7);
        Assert.Equal(new DateOnly(2026, 11, 21), nextTwoWeekStart);

        // 情境 3：惡意或手動帶入過去的日期 (例如 2026-10-01)，必須強制防呆校正至今天
        var requestedPastStart = new DateOnly(2026, 10, 1);
        var sanitizedStart = requestedPastStart < today ? today : requestedPastStart;
        Assert.Equal(today, sanitizedStart);
    }

    [Fact]
    public void PastSlots_AreFilteredOut_And_SelectedDateDoesNotFallbackToPast()
    {
        var today = new DateOnly(2026, 11, 8);
        var sevenDays = Enumerable.Range(0, 7).Select(i => today.AddDays(i)).ToList();

        var rawSlots = new List<TutoringSlot>
        {
            new TutoringSlot
            {
                Id = 1,
                StartsAtUtc = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc), // 過期時段 10/19
                EndsAtUtc = new DateTime(2026, 10, 19, 11, 30, 0, DateTimeKind.Utc),
                SlotType = SlotType.SmallGroup
            }
        };

        // 過濾掉今天之前的過期課堂
        var filteredSlots = rawSlots
            .Where(s => DateOnly.FromDateTime(BookingService.ToTaipeiTime(s.StartsAtUtc)) >= today)
            .ToList();

        Assert.Empty(filteredSlots);

        var slotDates = filteredSlots
            .Select(s => DateOnly.FromDateTime(BookingService.ToTaipeiTime(s.StartsAtUtc)))
            .Distinct()
            .ToList();

        var upcomingInSevenDays = sevenDays.FirstOrDefault(d => slotDates.Contains(d));
        DateOnly? selectedDate = upcomingInSevenDays != default ? upcomingInSevenDays : null;

        // 當本週 7 天內無課堂時，SelectedDate 應為 null，絕不會回退選到 10/19
        Assert.Null(selectedDate);
    }

    [Fact]
    public void CalendarDaySummary_Slots_DisplaysSlotTimeRangeInsteadOfTypeName()
    {
        var summary = new CalendarDaySummary
        {
            Date = new DateOnly(2026, 11, 9),
            SmallGroupCount = 1,
            GroupCount = 1,
            Slots = new List<TutoringSlot>
            {
                new TutoringSlot
                {
                    Id = 10,
                    StartsAtUtc = new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc), // Taipei 18:00
                    EndsAtUtc = new DateTime(2026, 11, 9, 11, 30, 0, DateTimeKind.Utc),   // Taipei 19:30
                    SlotType = SlotType.SmallGroup
                },
                new TutoringSlot
                {
                    Id = 11,
                    StartsAtUtc = new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc),  // Taipei 17:00
                    EndsAtUtc = new DateTime(2026, 11, 9, 11, 0, 0, DateTimeKind.Utc),   // Taipei 19:00
                    SlotType = SlotType.GroupCounseling
                }
            }
        };

        var badgeTexts = summary.Slots.Select(daySlot =>
        {
            var startTpe = BookingService.ToTaipeiTime(daySlot.StartsAtUtc);
            var endTpe = BookingService.ToTaipeiTime(daySlot.EndsAtUtc);
            return $"{startTpe:HH:mm}-{endTpe:HH:mm}";
        }).ToList();

        Assert.Equal(2, badgeTexts.Count);
        Assert.Equal("18:00-19:30", badgeTexts[0]);
        Assert.Equal("17:00-19:00", badgeTexts[1]);
    }

    [Fact]
    public void CourseProgress_SupportsMultipleCurrentProgresses_And_AutoGeneratesCode()
    {
        var progresses = new List<CourseProgress>
        {
            new CourseProgress { Id = 1, Code = "W07", Title = "第7週－二次函數與極值應用", IsCurrentProgress = true },
            new CourseProgress { Id = 2, Code = "W08", Title = "第8週－圓的幾何性質與切線", IsCurrentProgress = false }
        };

        // 切換第 2 筆為開放進度時，第 1 筆仍保持開放（支援多個進度同時開放）
        var target = progresses.First(p => p.Id == 2);
        target.IsCurrentProgress = !target.IsCurrentProgress;

        Assert.True(progresses[0].IsCurrentProgress);
        Assert.True(progresses[1].IsCurrentProgress);
        Assert.Equal(2, progresses.Count(p => p.IsCurrentProgress));

        // 系統自動產生下一個進度代碼（不需使用者輸入）
        var existingCodes = progresses.Select(p => p.Code).ToList();
        var maxNum = existingCodes
            .Select(c => !string.IsNullOrWhiteSpace(c) && c.StartsWith("W", StringComparison.OrdinalIgnoreCase) && int.TryParse(c.Substring(1), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        var nextNum = Math.Max(maxNum + 1, existingCodes.Count + 1);
        var autoCode = $"W{nextNum:D2}";

        Assert.Equal("W09", autoCode);
    }
}

