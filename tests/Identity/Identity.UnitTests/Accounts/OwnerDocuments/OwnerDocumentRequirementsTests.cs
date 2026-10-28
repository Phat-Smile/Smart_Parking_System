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

    // F3: an owner type that is not one of the enum values used to get an empty checklist, so nothing was ever missing.
    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void An_owner_type_that_is_not_defined_is_rejected_instead_of_getting_an_empty_checklist(int value)
    {
        var undefined = (OwnerType)value;

        Assert.Throws<ArgumentOutOfRangeException>(() => _requirements.GetRequirements(undefined));
        Assert.Throws<ArgumentOutOfRangeException>(() => _requirements.GetMissingRequiredDocuments(undefined, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => _requirements.ResolveDocumentType(undefined, "PremisesRightProof"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _requirements.IsAllowed(undefined, "PremisesRightProof"));
    }

    // F8: the checklist matches a document type in any casing, so it has to hand back its own spelling for storage and lookup.
    [Theory]
    [InlineData("PremisesRightProof", "PremisesRightProof")]
    [InlineData("premisesrightproof", "PremisesRightProof")]
    [InlineData("  PREMISESRIGHTPROOF  ", "PremisesRightProof")]
    [InlineData("authorizationletter", "AuthorizationLetter")]
    public void ResolveDocumentType_returns_the_canonical_spelling_of_a_type_on_the_checklist(string input, string expected)
    {
        Assert.Equal(expected, _requirements.ResolveDocumentType(OwnerType.Individual, input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownType")]
    [InlineData("EnterpriseRegistration")]
    [InlineData("Premises RightProof")]
    public void ResolveDocumentType_is_null_for_blank_unknown_or_other_checklists(string? input)
    {
        Assert.Null(_requirements.ResolveDocumentType(OwnerType.Individual, input));
    }

    // A document kept after the owner type changed is outside the new checklist, but it must still be found and deletable.
    [Theory]
    [InlineData("enterpriseregistration", "EnterpriseRegistration")]
    [InlineData("HOUSEHOLDBUSINESSREGISTRATION", "HouseholdBusinessRegistration")]
    [InlineData("premisesrightproof", "PremisesRightProof")]
    [InlineData(" authorizationLetter ", "AuthorizationLetter")]
    public void ResolveKnownDocumentType_finds_the_canonical_spelling_across_all_checklists(string input, string expected)
    {
        Assert.Equal(expected, _requirements.ResolveKnownDocumentType(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nope")]
    public void ResolveKnownDocumentType_is_null_for_blank_or_unknown_types(string? input)
    {
        Assert.Null(_requirements.ResolveKnownDocumentType(input));
    }
}
