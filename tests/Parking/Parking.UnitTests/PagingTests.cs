using ParkingSystem.Parking.Application.Common.Paging;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class PagingTests
{
    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-5, -1, 1, 20)]
    [InlineData(3, 50, 3, 50)]
    [InlineData(2, 100, 2, 100)]
    [InlineData(2, 101, 2, 100)]
    [InlineData(1, int.MaxValue, 1, 100)]
    [InlineData(int.MaxValue, 20, int.MaxValue, 20)]
    public void Normalize_falls_back_to_the_defaults_and_caps_the_page_size(int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        Assert.Equal((expectedPage, expectedPageSize), Paging.Normalize(page, pageSize));
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(3, 100, 200)]
    [InlineData(0, 20, 0)]
    [InlineData(int.MaxValue, 100, int.MaxValue)]
    public void Skip_is_the_offset_of_the_first_row_of_the_page_and_never_overflows(int page, int pageSize, int expectedSkip)
    {
        Assert.Equal(expectedSkip, Paging.Skip(page, pageSize));
    }
}
