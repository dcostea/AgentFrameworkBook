using RagShared;
using Xunit;

namespace RagSamples.Tests;

public sealed class SampleDataTests
{
    [Fact]
    public void Corpus_HasUniqueIdsAndDocumentsForEveryDependencyEndpoint()
    {
        Assert.NotEmpty(SampleData.Documents);
        Assert.Equal(SampleData.Documents.Count, SampleData.Documents.Select(document => document.Id).Distinct().Count());
        Assert.NotEmpty(SampleData.Dependencies);
        Assert.All(SampleData.Dependencies, edge =>
        {
            Assert.Contains(SampleData.Documents, document => document.Service == edge.From);
            Assert.Contains(SampleData.Documents, document => document.Service == edge.To);
        });
    }

    [Fact]
    public void SafetyFixture_MotorAndNavigationAreWithinTwoHopsButMaintenanceIsThirdHop()
    {
        Assert.Contains(new Dependency("MotorService", "SafetyService"), SampleData.Dependencies);
        Assert.Contains(new Dependency("NavigationService", "MotorService"), SampleData.Dependencies);
        Assert.Contains(new Dependency("MaintenanceService", "NavigationService"), SampleData.Dependencies);

        // Inspect the curated fixture, not a replacement for the Neo4j traversal implementation.
        string[] firstHop = SampleData.Dependencies.Where(edge => edge.To == "SafetyService")
            .Select(edge => edge.From).ToArray();
        HashSet<string> withinTwoHops = ["SafetyService", .. firstHop,
            .. SampleData.Dependencies.Where(edge => firstHop.Contains(edge.To)).Select(edge => edge.From)];
        string[] documentIds = SampleData.Documents
            .Where(document => withinTwoHops.Contains(document.Service))
            .Select(document => document.Id).Order().ToArray();

        Assert.Equal(["MotorService"], firstHop);
        Assert.Equal(["doc-1", "doc-2", "doc-3"], documentIds);
        Assert.DoesNotContain("MaintenanceService", withinTwoHops);
        Assert.Contains("MaintenanceService", SampleData.Dependencies
            .Where(edge => withinTwoHops.Contains(edge.To)).Select(edge => edge.From));
    }
}
