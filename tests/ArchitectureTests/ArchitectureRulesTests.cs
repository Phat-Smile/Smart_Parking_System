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
            AssertProjectReferences(service, $"{service}.WebAPI", $"{service}.Application", $"{service}.Persistence", $"{service}.Infrastructure", "ParkingSystem.BuildingBlocks", "ParkingSystem.AspNetCore");
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
    public void AspNetCore_foundation_is_referenced_only_by_http_hosts()
    {
        var foundation = Path.Combine(Root, "src", "BuildingBlocks", "ParkingSystem.AspNetCore", "ParkingSystem.AspNetCore.csproj");
        Assert.True(File.Exists(foundation));
        Assert.Equal(
            ["ParkingSystem.BuildingBlocks"],
            ReadReferences(foundation)
                .Select(reference => Path.GetFileNameWithoutExtension(reference))
                .ToArray());

        foreach (var service in Services)
        {
            foreach (var layer in new[] { $"{service}.Domain", $"{service}.Application", $"{service}.Persistence", $"{service}.Infrastructure" })
            {
                var project = ProjectPath(service, layer);
                Assert.DoesNotContain(ReadReferences(project), reference => reference.Contains("ParkingSystem.AspNetCore", StringComparison.OrdinalIgnoreCase));
                var projectXml = XDocument.Load(project);
                Assert.DoesNotContain(projectXml.Descendants().Where(element => element.Name.LocalName == "PackageReference"), element =>
                    ((string?)element.Attribute("Include"))?.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) == true);
                Assert.DoesNotContain(projectXml.Descendants().Where(element => element.Name.LocalName == "FrameworkReference"), element =>
                    ((string?)element.Attribute("Include"))?.Equals("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase) == true);
            }
        }

        var gateway = Path.Combine(Root, "src", "Gateway", "ParkingSystem.Gateway.csproj");
        Assert.Contains(ReadReferences(gateway), reference => reference.Contains("ParkingSystem.AspNetCore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Jwt_validation_dependencies_and_transport_types_stay_in_the_http_foundation()
    {
        var sourceProjects = Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories).ToArray();
        var jwtProjects = sourceProjects
            .Where(project => ReadPackageReferences(project).Contains("Microsoft.AspNetCore.Authentication.JwtBearer", StringComparer.Ordinal))
            .Select(project => Path.GetFullPath(project))
            .ToArray();
        var foundation = Path.GetFullPath(Path.Combine(Root, "src", "BuildingBlocks", "ParkingSystem.AspNetCore", "ParkingSystem.AspNetCore.csproj"));
        Assert.Equal([foundation], jwtProjects);

        foreach (var service in Services)
        {
            foreach (var layer in new[] { $"{service}.Domain", $"{service}.Application", $"{service}.Persistence", $"{service}.Infrastructure" })
            {
                var project = ProjectPath(service, layer);
                Assert.DoesNotContain("Microsoft.AspNetCore.Authentication.JwtBearer", ReadPackageReferences(project));
            }

            var applicationRoot = Path.Combine(Root, "src", "Services", service, $"{service}.Application");
            var applicationCode = string.Join(Environment.NewLine,
                Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            Assert.DoesNotContain("HttpContext", applicationCode, StringComparison.Ordinal);
            Assert.DoesNotContain("ClaimsPrincipal", applicationCode, StringComparison.Ordinal);
            Assert.DoesNotContain("JwtSecurityToken", applicationCode, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpRequest", applicationCode, StringComparison.Ordinal);
        }

        var genericBuildingBlocks = Path.Combine(Root, "src", "BuildingBlocks", "ParkingSystem.BuildingBlocks.csproj");
        Assert.DoesNotContain("Microsoft.AspNetCore.Authentication.JwtBearer", ReadPackageReferences(genericBuildingBlocks));
    }

    [Fact]
    public void Local_private_jwt_key_is_gitignored_and_never_enters_docker_context_or_validator_mounts()
    {
        var gitIgnore = File.ReadAllText(Path.Combine(Root, ".gitignore"));
        var dockerIgnore = File.ReadAllText(Path.Combine(Root, ".dockerignore"));
        var compose = File.ReadAllText(Path.Combine(Root, "docker-compose.yml"));
        var envExample = File.ReadAllText(Path.Combine(Root, ".env.example"));

        Assert.Contains(".local/", gitIgnore, StringComparison.Ordinal);
        Assert.Contains(".local/", dockerIgnore, StringComparison.Ordinal);
        Assert.Contains("*.pem", dockerIgnore, StringComparison.Ordinal);
        Assert.Contains("public.pem:/run/parking-system/security/jwt/public.pem:ro", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("private.pem", compose, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JWT_PRIVATE_KEY", envExample, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", envExample, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void Grpc_transport_dependencies_are_confined_to_web_api_hosts()
    {
        var grpcPackages = new[]
        {
            "Grpc.AspNetCore.Server",
            "Grpc.AspNetCore.HealthChecks",
            "Grpc.AspNetCore.Server.Reflection"
        };

        foreach (var service in Services)
        {
            var webApiPackages = ReadPackageReferences(ProjectPath(service, $"{service}.WebAPI"));
            foreach (var package in grpcPackages)
                Assert.Contains(package, webApiPackages);

            foreach (var layer in new[] { $"{service}.Domain", $"{service}.Application", $"{service}.Persistence", $"{service}.Infrastructure" })
            {
                var packages = ReadPackageReferences(ProjectPath(service, layer));
                Assert.DoesNotContain(packages, package => package.StartsWith("Grpc.", StringComparison.OrdinalIgnoreCase));
            }
        }

        var buildingBlocks = Path.Combine(Root, "src", "BuildingBlocks", "ParkingSystem.BuildingBlocks.csproj");
        Assert.DoesNotContain(ReadPackageReferences(buildingBlocks), package =>
            package.StartsWith("Grpc.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OpenTelemetry_packages_stay_out_of_business_and_generic_building_block_layers()
    {
        foreach (var service in Services)
        {
            foreach (var layer in new[] { $"{service}.Domain", $"{service}.Application", $"{service}.Persistence" })
            {
                var packages = ReadPackageReferences(ProjectPath(service, layer));
                Assert.DoesNotContain(packages, package => package.StartsWith("OpenTelemetry.", StringComparison.OrdinalIgnoreCase));
            }
        }

        var genericBuildingBlocks = Path.Combine(Root, "src", "BuildingBlocks", "ParkingSystem.BuildingBlocks.csproj");
        Assert.DoesNotContain(ReadPackageReferences(genericBuildingBlocks), package =>
            package.StartsWith("OpenTelemetry.", StringComparison.OrdinalIgnoreCase));

        var hostFoundation = Path.Combine(Root, "src", "BuildingBlocks", "ParkingSystem.AspNetCore", "ParkingSystem.AspNetCore.csproj");
        Assert.Contains(ReadPackageReferences(hostFoundation), package => package == "OpenTelemetry.Extensions.Hosting");
    }

    [Fact]
    public void Each_net_host_registers_its_unique_canonical_telemetry_service_name_once()
    {
        var hosts = new Dictionary<string, string>
        {
            [Path.Combine(Root, "src", "Gateway", "Program.cs")] = "parking-gateway",
            [ProjectPath("Identity", "Identity.WebAPI").Replace("Identity.WebAPI.csproj", "Program.cs", StringComparison.Ordinal)] = "identity-api",
            [ProjectPath("Parking", "Parking.WebAPI").Replace("Parking.WebAPI.csproj", "Program.cs", StringComparison.Ordinal)] = "parking-api",
            [ProjectPath("Booking", "Booking.WebAPI").Replace("Booking.WebAPI.csproj", "Program.cs", StringComparison.Ordinal)] = "booking-api",
            [ProjectPath("Payment", "Payment.WebAPI").Replace("Payment.WebAPI.csproj", "Program.cs", StringComparison.Ordinal)] = "payment-api",
            [ProjectPath("Notification", "Notification.WebAPI").Replace("Notification.WebAPI.csproj", "Program.cs", StringComparison.Ordinal)] = "notification-api"
        };

        Assert.Equal(6, hosts.Values.Distinct(StringComparer.Ordinal).Count());
        foreach (var (program, serviceName) in hosts)
        {
            var source = File.ReadAllText(program);
            Assert.Equal(1, CountOccurrences(source, "AddParkingSystemTelemetry("));
            Assert.Contains($"AddParkingSystemTelemetry(\"{serviceName}\")", source, StringComparison.Ordinal);
        }
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var start = 0;
        while ((start = source.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }
        return count;
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
