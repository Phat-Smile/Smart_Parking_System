using System.Xml.Linq;
using Xunit;

namespace ParkingSystem.Tests.Architecture;

public sealed class ArchitectureRulesTests
{
    private static readonly string[] Services = ["Identity", "Parking", "Booking", "Payment", "Notification"];
    private static string Root => FindRepositoryRoot();

    [Fact]
    public void Domain_projects_have_no_service_layer_project_references()
    {
        foreach (var service in Services)
        {
            var project = ProjectPath(service, $"{service}.Domain");
            var references = ReadReferences(project);
            Assert.Empty(references);
        }
    }

    [Fact]
    public void Application_projects_do_not_reference_outer_layers()
    {
        foreach (var service in Services)
        {
            var project = ProjectPath(service, $"{service}.Application");
            var references = ReadReferences(project);
            var forbidden = references.Where(reference =>
                reference.Contains(".Persistence.csproj", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(".Infrastructure.csproj", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(".WebAPI.csproj", StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.Empty(forbidden);
        }
    }

    [Fact]
    public void Persistence_and_infrastructure_projects_do_not_reference_each_other()
    {
        foreach (var service in Services)
        {
            var persistenceReferences = ReadReferences(ProjectPath(service, $"{service}.Persistence"));
            var infrastructureReferences = ReadReferences(ProjectPath(service, $"{service}.Infrastructure"));

            Assert.DoesNotContain(persistenceReferences, reference => reference.Contains(".Infrastructure.csproj", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(infrastructureReferences, reference => reference.Contains(".Persistence.csproj", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Service_layer_project_references_match_the_expected_graph()
    {
        foreach (var service in Services)
        {
            AssertProjectReferences(service, $"{service}.Domain");
            AssertProjectReferences(service, $"{service}.Application", $"{service}.Domain", "ParkingSystem.BuildingBlocks");
            AssertProjectReferences(service, $"{service}.Persistence", $"{service}.Domain", $"{service}.Application");
            AssertProjectReferences(service, $"{service}.Infrastructure", $"{service}.Application", "ParkingSystem.BuildingBlocks");
            AssertProjectReferences(service, $"{service}.WebAPI", $"{service}.Application", $"{service}.Persistence", $"{service}.Infrastructure", "ParkingSystem.BuildingBlocks");
        }
    }

    [Fact]
    public void Business_service_projects_do_not_reference_another_service()
    {
        var servicesRoot = Path.Combine(Root, "src", "Services");
        foreach (var service in Services)
        {
            foreach (var project in Directory.EnumerateFiles(Path.Combine(servicesRoot, service), "*.csproj", SearchOption.AllDirectories))
            {
                foreach (var reference in ReadReferences(project))
                {
                    var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference));
                    var segments = Path.GetRelativePath(Root, target)
                        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                        .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 3 && segments[0] == "src" && segments[1] == "Services")
                        Assert.Equal(service, segments[2]);
                }
            }
        }
    }

    [Fact]
    public void Custom_outbox_and_inbox_are_used_without_production_consumers_or_MassTransit_state()
    {
        var productionFiles = Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var source = string.Join(Environment.NewLine, productionFiles.Select(File.ReadAllText));

        Assert.Contains("OutboxDispatcher", source, StringComparison.Ordinal);
        Assert.Contains("InboxConsumerFilter<TConsumer>", source, StringComparison.Ordinal);
        Assert.Contains("IInboxExecutionCoordinator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseBusOutbox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseEntityFrameworkOutbox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddEntityFrameworkOutbox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InboxState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OutboxState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IConsumer<", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".AddIdempotentConsumer<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bus.AddConsumer<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReceiveEndpoint(", source, StringComparison.Ordinal);
    }


    [Fact]
    public void Inbox_application_abstractions_are_transport_and_persistence_neutral()
    {
        foreach (var service in Services)
        {
            var inboxFiles = Directory.EnumerateFiles(
                Path.Combine(Root, "src", "Services", service, $"{service}.Application", "Common", "Messaging", "Inbox"),
                "*.cs",
                SearchOption.AllDirectories);
            var source = string.Join(Environment.NewLine, inboxFiles.Select(File.ReadAllText));

            Assert.Contains("IInboxExecutionCoordinator", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MassTransit", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Npgsql", source, StringComparison.OrdinalIgnoreCase);
        }
    }



    [Fact]
    public void MassTransit_dependencies_are_confined_to_service_infrastructure_projects()
    {
        foreach (var service in Services)
        {
            var infrastructure = ProjectPath(service, $"{service}.Infrastructure");
            var infrastructurePackages = ReadPackageReferences(infrastructure);
            Assert.Contains("MassTransit", infrastructurePackages);
            Assert.Contains("MassTransit.RabbitMQ", infrastructurePackages);
            Assert.Contains("RabbitMQ.Client", infrastructurePackages);

            foreach (var layer in new[] { $"{service}.Domain", $"{service}.Application", $"{service}.Persistence", $"{service}.WebAPI" })
            {
                var packages = ReadPackageReferences(ProjectPath(service, layer));
                Assert.DoesNotContain(packages, package => package.Contains("MassTransit", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(packages, package => package.Contains("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase));
            }
        }

        var buildingBlocks = Path.Combine(Root, "src", "BuildingBlocks", "ParkingSystem.BuildingBlocks.csproj");
        Assert.DoesNotContain(ReadPackageReferences(buildingBlocks), package =>
            package.Contains("MassTransit", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase));
    }

    private static string ProjectPath(string service, string projectName) =>
        Path.Combine(Root, "src", "Services", service, projectName, $"{projectName}.csproj");

    private static string[] ReadReferences(string project)
    {
        Assert.True(File.Exists(project), $"Missing project file: {project}");
        return XDocument.Load(project)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .ToArray();
    }

    private static string[] ReadPackageReferences(string project)
    {
        Assert.True(File.Exists(project), $"Missing project file: {project}");
        return XDocument.Load(project)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .ToArray();
    }

    private static void AssertProjectReferences(string service, string projectName, params string[] expectedNames)
    {
        var project = ProjectPath(service, projectName);
        var actualNames = ReadReferences(project)
            .Select(reference => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference)))
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = expectedNames.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actualNames);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ParkingSystem.slnx")))
                    return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate ParkingSystem.slnx from the test working directory.");
    }
}
