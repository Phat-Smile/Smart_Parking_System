using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.OwnerDocuments;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Accounts.OwnerDocuments;

public sealed class OwnerDocumentRequirementsTests
{
    private readonly ConfigurationOwnerDocumentRequirements _requirements;

    public OwnerDocumentRequirementsTests()
    {
        var options = Options.Create(new OwnerDocumentsOptions());
        _requirements = new ConfigurationOwnerDocumentRequirements(options);
    }

    [Fact]
    public void GetRequirements_for_Individual_contains_expected_checklist()
    {
        var items = _requirements.GetRequirements(OwnerType.Individual);

        Assert.Contains(items, d => d.DocumentType == "PremisesRightProof" && d.Required);
        Assert.Contains(items, d => d.DocumentType == "AuthorizationLetter" && !d.Required);
        Assert.DoesNotContain(items, d => d.DocumentType == "EnterpriseRegistration");
    }

    [Fact]
    public void GetRequirements_for_HouseholdBusiness_contains_expected_checklist()
    {
        var items = _requirements.GetRequirements(OwnerType.HouseholdBusiness);

        Assert.Contains(items, d => d.DocumentType == "HouseholdBusinessRegistration" && d.Required);
        Assert.Contains(items, d => d.DocumentType == "PremisesRightProof" && d.Required);
        Assert.Contains(items, d => d.DocumentType == "AuthorizationLetter" && !d.Required);
        Assert.DoesNotContain(items, d => d.DocumentType == "EnterpriseRegistration");
    }

    [Fact]
    public void GetRequirements_for_Enterprise_contains_expected_checklist()
    {
        var items = _requirements.GetRequirements(OwnerType.Enterprise);

        Assert.Contains(items, d => d.DocumentType == "EnterpriseRegistration" && d.Required);
        Assert.Contains(items, d => d.DocumentType == "PremisesRightProof" && d.Required);
        Assert.Contains(items, d => d.DocumentType == "AuthorizationLetter" && !d.Required);
    }

    [Theory]
    [InlineData("PremisesRightProof", true)]
    [InlineData("AuthorizationLetter", true)]
    [InlineData("HouseholdBusinessRegistration", false)]
    [InlineData("EnterpriseRegistration", false)]
    [InlineData("UnknownType", false)]
    public void IsAllowed_for_Individual_validates_types(string docType, bool expected)
    {
        var result = _requirements.IsAllowed(OwnerType.Individual, docType);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetMissingRequiredDocuments_returns_missing_when_required_documents_are_lacking()
    {
        string[] uploaded = [];

        var missing = _requirements.GetMissingRequiredDocuments(OwnerType.Individual, uploaded);

        Assert.NotEmpty(missing);
        Assert.Contains("PremisesRightProof", missing);
    }

    [Fact]
    public void GetMissingRequiredDocuments_returns_empty_when_all_required_documents_are_present()
    {
        string[] uploaded = ["PremisesRightProof"];

        var missing = _requirements.GetMissingRequiredDocuments(OwnerType.Individual, uploaded);

        Assert.Empty(missing);
    }

    [Fact]
    public void GetMissingRequiredDocuments_with_optional_documents_still_passes()
    {
        string[] uploaded = ["PremisesRightProof", "AuthorizationLetter"];

        var missing = _requirements.GetMissingRequiredDocuments(OwnerType.Individual, uploaded);

        Assert.Empty(missing);
    }
}
