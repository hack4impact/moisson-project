using MoissonMontreal.Api.Donors;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Tests.Api;

public class DonorValidatorTests
{
    [Fact]
    public void TryNormalize_AcceptsAFullProfile_AndTrimsEachField()
    {
        var request = new DonorRequest(
            "  Boulangerie Saint-Laurent  ",
            " organization ",
            " 123 rue Saint-Laurent ",
            " Montréal ",
            " QC ",
            " H2X 1Y4 ",
            " Marie Tremblay ",
            " dons@boulangerie.ca ",
            " 514-555-0100 ",
            " Livre le lundi ",
            " C-100 ");

        var ok = DonorValidator.TryNormalize(request, out var fields, out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.Equal(
            new DonorFields(
                "Boulangerie Saint-Laurent",
                DonorTypes.Organization,
                "123 rue Saint-Laurent",
                "Montréal",
                "QC",
                "H2X 1Y4",
                "Marie Tremblay",
                "dons@boulangerie.ca",
                "514-555-0100",
                "Livre le lundi",
                "C-100"),
            fields);
    }

    [Theory]
    [InlineData(DonorTypes.Individual)]
    [InlineData(DonorTypes.Organization)]
    public void TryNormalize_AcceptsEachType(string type)
    {
        var ok = DonorValidator.TryNormalize(Valid() with { Type = type }, out var fields, out _);

        Assert.True(ok);
        Assert.Equal(type, fields.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_StoresBlankOptionalFieldsAsNull(string? blank)
    {
        var request = Valid() with
        {
            ContactPerson = blank,
            Email = blank,
            Phone = blank,
            Notes = blank,
            BusinessCentralCode = blank,
        };

        var ok = DonorValidator.TryNormalize(request, out var fields, out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.Null(fields.ContactPerson);
        Assert.Null(fields.Email);
        Assert.Null(fields.Phone);
        Assert.Null(fields.Notes);
        Assert.Null(fields.BusinessCentralCode);
    }

    [Theory]
    [InlineData(nameof(DonorRequest.Name), null)]
    [InlineData(nameof(DonorRequest.Name), "")]
    [InlineData(nameof(DonorRequest.Name), "   ")]
    [InlineData(nameof(DonorRequest.Address), null)]
    [InlineData(nameof(DonorRequest.Address), "")]
    [InlineData(nameof(DonorRequest.Address), "   ")]
    [InlineData(nameof(DonorRequest.City), null)]
    [InlineData(nameof(DonorRequest.City), "")]
    [InlineData(nameof(DonorRequest.City), "   ")]
    [InlineData(nameof(DonorRequest.Province), null)]
    [InlineData(nameof(DonorRequest.Province), "")]
    [InlineData(nameof(DonorRequest.Province), "   ")]
    [InlineData(nameof(DonorRequest.PostalCode), null)]
    [InlineData(nameof(DonorRequest.PostalCode), "")]
    [InlineData(nameof(DonorRequest.PostalCode), "   ")]
    public void TryNormalize_RejectsAMissingRequiredField_WithOneMessage(string field, string? value)
    {
        var ok = DonorValidator.TryNormalize(With(field, value), out _, out var errors);

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal(field, error.Key);
        Assert.Equal([$"{field} is required."], error.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("company")]
    [InlineData("Organization")]
    [InlineData("INDIVIDUAL")]
    public void TryNormalize_RejectsATypeThatIsNotIndividualOrOrganization(string? type)
    {
        var ok = DonorValidator.TryNormalize(Valid() with { Type = type }, out _, out var errors);

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal(nameof(DonorRequest.Type), error.Key);
        Assert.Equal(["Type must be individual or organization."], error.Value);
    }

    [Theory]
    [InlineData(nameof(DonorRequest.Name), Donor.NameMaxLength)]
    [InlineData(nameof(DonorRequest.Address), Donor.AddressMaxLength)]
    [InlineData(nameof(DonorRequest.City), Donor.CityMaxLength)]
    [InlineData(nameof(DonorRequest.Province), Donor.ProvinceMaxLength)]
    [InlineData(nameof(DonorRequest.PostalCode), Donor.PostalCodeMaxLength)]
    [InlineData(nameof(DonorRequest.ContactPerson), Donor.ContactPersonMaxLength)]
    [InlineData(nameof(DonorRequest.Phone), Donor.PhoneMaxLength)]
    [InlineData(nameof(DonorRequest.Notes), Donor.NotesMaxLength)]
    [InlineData(nameof(DonorRequest.BusinessCentralCode), Donor.BusinessCentralCodeMaxLength)]
    public void TryNormalize_AcceptsTheLongestValue_AndRejectsOneMoreCharacter(string field, int maxLength)
    {
        Assert.True(DonorValidator.TryNormalize(With(field, new string('a', maxLength)), out _, out _));

        var ok = DonorValidator.TryNormalize(With(field, new string('a', maxLength + 1)), out _, out var errors);

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal(field, error.Key);
        Assert.Equal([$"{field} must be {maxLength} characters or fewer."], error.Value);
    }

    [Fact]
    public void TryNormalize_AcceptsTheLongestEmail_AndRejectsOneMoreCharacter()
    {
        var domain = "@moisson.ca";
        var longest = new string('a', Donor.EmailMaxLength - domain.Length) + domain;
        Assert.True(DonorValidator.TryNormalize(Valid() with { Email = longest }, out _, out _));

        var ok = DonorValidator.TryNormalize(Valid() with { Email = "a" + longest }, out _, out var errors);

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal(nameof(DonorRequest.Email), error.Key);
        Assert.Equal([$"Email must be {Donor.EmailMaxLength} characters or fewer."], error.Value);
    }

    [Theory]
    [InlineData("dons")]
    [InlineData("dons@")]
    [InlineData("@boulangerie.ca")]
    [InlineData("dons boulangerie@boulangerie.ca")]
    [InlineData("Boulangerie <dons@boulangerie.ca>")]
    [InlineData("dons@boulangerie.ca, info@boulangerie.ca")]
    public void TryNormalize_RejectsAnInvalidEmail(string email)
    {
        var ok = DonorValidator.TryNormalize(Valid() with { Email = email }, out _, out var errors);

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal(nameof(DonorRequest.Email), error.Key);
        Assert.Equal(["Email must be a valid email address."], error.Value);
    }

    [Fact]
    public void TryNormalize_ReportsEveryProblem_WithOneMessagePerField()
    {
        var request = new DonorRequest(null, "company", "", " ", null, "", null, "not-an-email", null, null, null);

        var ok = DonorValidator.TryNormalize(request, out _, out var errors);

        Assert.False(ok);
        Assert.Equal(
            [
                nameof(DonorRequest.Address),
                nameof(DonorRequest.City),
                nameof(DonorRequest.Email),
                nameof(DonorRequest.Name),
                nameof(DonorRequest.PostalCode),
                nameof(DonorRequest.Province),
                nameof(DonorRequest.Type),
            ],
            errors.Keys.Order());
        Assert.All(errors.Values, messages => Assert.Single(messages));
    }

    private static DonorRequest Valid() => new(
        "Boulangerie Saint-Laurent",
        DonorTypes.Organization,
        "123 rue Saint-Laurent",
        "Montréal",
        "QC",
        "H2X 1Y4",
        null,
        null,
        null,
        null,
        null);

    private static DonorRequest With(string field, string? value) => field switch
    {
        nameof(DonorRequest.Name) => Valid() with { Name = value },
        nameof(DonorRequest.Address) => Valid() with { Address = value },
        nameof(DonorRequest.City) => Valid() with { City = value },
        nameof(DonorRequest.Province) => Valid() with { Province = value },
        nameof(DonorRequest.PostalCode) => Valid() with { PostalCode = value },
        nameof(DonorRequest.ContactPerson) => Valid() with { ContactPerson = value },
        nameof(DonorRequest.Phone) => Valid() with { Phone = value },
        nameof(DonorRequest.Notes) => Valid() with { Notes = value },
        nameof(DonorRequest.BusinessCentralCode) => Valid() with { BusinessCentralCode = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };
}
