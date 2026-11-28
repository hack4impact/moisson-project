using MoissonMontreal.Api.Users;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Tests.Api;

public class UserValidatorTests
{
    [Fact]
    public void TryNormalize_TrimsEachField_AndPutsTheEmailInLowercase()
    {
        var request = new UserRequest("  Marie Tremblay  ", "  Marie.Tremblay@MoissonMontreal.org ", " Finance ");

        var ok = UserValidator.TryNormalize(request, out var fields, out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.Equal(new UserFields("Marie Tremblay", "marie.tremblay@moissonmontreal.org", RoleCodes.Finance), fields);
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Finance)]
    [InlineData(RoleCodes.Admin)]
    public void TryNormalize_AcceptsEachRole(string role)
    {
        var ok = UserValidator.TryNormalize(Valid() with { Role = role }, out var fields, out _);

        Assert.True(ok);
        Assert.Equal(role, fields.Role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_RequiresTheName(string? name)
    {
        AssertSingleError(Valid() with { DisplayName = name }, nameof(UserRequest.DisplayName), "DisplayName is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_RequiresTheEmail(string? email)
    {
        AssertSingleError(Valid() with { Email = email }, nameof(UserRequest.Email), "Email is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Manager")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Supply Director")]
    public void TryNormalize_RefusesARoleThatIsNotExactlyARoleCode(string? role)
    {
        AssertSingleError(Valid() with { Role = role }, nameof(UserRequest.Role), "Role must be one of: Supply, SupplyDirector, Finance, Admin.");
    }

    [Theory]
    [InlineData("marie")]
    [InlineData("marie@")]
    [InlineData("@moissonmontreal.org")]
    [InlineData("marie tremblay@moissonmontreal.org")]
    [InlineData("Marie <marie@moissonmontreal.org>")]
    [InlineData("marie@moissonmontreal.org, luc@moissonmontreal.org")]
    public void TryNormalize_RefusesAnInvalidEmail(string email)
    {
        AssertSingleError(Valid() with { Email = email }, nameof(UserRequest.Email), "Email must be a valid email address.");
    }

    [Fact]
    public void TryNormalize_AcceptsTheLongestName_AndRefusesOneMoreCharacter()
    {
        Assert.True(UserValidator.TryNormalize(Valid() with { DisplayName = new string('a', User.DisplayNameMaxLength) }, out _, out _));

        AssertSingleError(
            Valid() with { DisplayName = new string('a', User.DisplayNameMaxLength + 1) },
            nameof(UserRequest.DisplayName),
            "DisplayName must be 200 characters or fewer.");
    }

    [Fact]
    public void TryNormalize_AcceptsTheLongestEmail_AndRefusesOneMoreCharacter()
    {
        const string domain = "@moissonmontreal.org";
        var longest = new string('a', User.EmailMaxLength - domain.Length) + domain;
        Assert.True(UserValidator.TryNormalize(Valid() with { Email = longest }, out _, out _));

        AssertSingleError(Valid() with { Email = "a" + longest }, nameof(UserRequest.Email), "Email must be 256 characters or fewer.");
    }

    [Fact]
    public void TryNormalize_GivesOneMessagePerField_WhenSeveralFieldsAreWrong()
    {
        var ok = UserValidator.TryNormalize(new UserRequest(" ", "not-an-email", "Manager"), out _, out var errors);

        Assert.False(ok);
        Assert.Equal(
            [nameof(UserRequest.DisplayName), nameof(UserRequest.Email), nameof(UserRequest.Role)],
            errors.Keys.Order());
        Assert.All(errors.Values, messages => Assert.Single(messages));
    }

    private static UserRequest Valid() => new("Marie Tremblay", "marie.tremblay@moissonmontreal.org", RoleCodes.Supply);

    private static void AssertSingleError(UserRequest request, string field, string message)
    {
        var ok = UserValidator.TryNormalize(request, out _, out var errors);

        Assert.False(ok);
        var error = Assert.Single(errors);
        Assert.Equal(field, error.Key);
        Assert.Equal([message], error.Value);
    }
}
