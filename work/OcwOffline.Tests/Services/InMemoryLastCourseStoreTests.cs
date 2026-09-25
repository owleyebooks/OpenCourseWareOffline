using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class InMemoryLastCourseStoreTests
{
    [Fact]
    public void GetLastCourseId_BeforeAnySet_ReturnsNull()
    {
        new InMemoryLastCourseStore().GetLastCourseId().Should().BeNull(
            because: "nothing has been stored yet");
    }

    [Fact]
    public void SetLastCourseId_ThenGet_ReturnsStoredId()
    {
        var store = new InMemoryLastCourseStore();

        store.SetLastCourseId("6-0001");

        store.GetLastCourseId().Should().Be("6-0001",
            because: "the stored id must round-trip");
    }

    [Fact]
    public void Clear_RemovesStoredId()
    {
        var store = new InMemoryLastCourseStore();
        store.SetLastCourseId("6-0001");

        store.Clear();

        store.GetLastCourseId().Should().BeNull(
            because: "clear must forget the stored id");
    }
}
