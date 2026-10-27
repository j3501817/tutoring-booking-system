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
}

