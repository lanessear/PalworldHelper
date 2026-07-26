using PalworldHelper;

namespace PalworldHelper.Core.Tests;

public sealed class BreedingServiceTests
{
    [Fact]
    public void FindAllShortestPathsFromAvailable_ReturnsEveryShortestChain_WhenNoStartSelected()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"breeding-service-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempPath, """
                {
                  "names": ["A", "B", "C", "D"],
                  "results": [
                    [0, 1, 2],
                    [0, 2, 3],
                    [1, 2, 3]
                  ]
                }
                """);

            var service = new BreedingService();
            service.Load(tempPath);

            var paths = service.FindAllShortestPathsFromAvailable("D", null);

            Assert.NotNull(paths);
            Assert.Equal(2, paths.Count);
            Assert.Contains(paths, path => path.Count == 1 && path[0].Parent == "A" && path[0].Mate == "C" && path[0].Child == "D");
            Assert.Contains(paths, path => path.Count == 1 && path[0].Parent == "B" && path[0].Mate == "C" && path[0].Child == "D");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
