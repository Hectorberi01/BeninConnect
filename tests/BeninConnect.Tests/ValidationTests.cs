using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.ViewModels;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Feed.ViewModels;

namespace BeninConnect.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData("demo@beninconnect.test", true)]
    [InlineData("pas-un-email", false)]
    [InlineData("", false)]
    public void Email_validation(string email, bool valid) =>
        Assert.Equal(valid, Validation.Email(email) is null);

    [Theory]
    [InlineData("Demo123!", true)]
    [InlineData("court1", false)]
    [InlineData("sanschiffre", false)]
    public void Password_validation(string password, bool valid) =>
        Assert.Equal(valid, Validation.Password(password) is null);

    [Fact]
    public async Task Login_with_wrong_password_shows_error_and_stays_on_screen()
    {
        using var host = new TestHost();
        var vm = await host.OpenAsync<LoginViewModel>();
        vm.Email = "demo@beninconnect.test";
        vm.Password = "mauvais";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.NotNull(vm.FormError);
        Assert.Same(vm, host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Login_with_empty_fields_reports_field_errors()
    {
        using var host = new TestHost();
        var vm = await host.OpenAsync<LoginViewModel>();

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.NotNull(vm.EmailError);
        Assert.NotNull(vm.PasswordError);
    }

    [Fact]
    public async Task Register_requires_matching_passwords_and_terms()
    {
        using var host = new TestHost();
        var vm = await host.OpenAsync<RegisterViewModel>();
        vm.FirstName = "Awa";
        vm.LastName = "Test";
        vm.Email = "awa@exemple.test";
        vm.Country = "France";
        vm.City = "Lyon";
        vm.Password = "Motdepasse1";
        vm.ConfirmPassword = "Autre1234";

        Assert.False(vm.Validate());
        Assert.NotNull(vm.ConfirmPasswordError);
        Assert.NotNull(vm.TermsError);
    }

    [Fact]
    public async Task Create_community_validates_required_fields()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<CreateCommunityViewModel>();
        vm.Name = "AB";
        vm.Description = "court";
        vm.City = "";

        Assert.False(vm.Validate());
        Assert.NotNull(vm.NameError);
        Assert.NotNull(vm.DescriptionError);
        Assert.NotNull(vm.CategoryError);
        Assert.NotNull(vm.CityError);
    }

    [Fact]
    public async Task Create_event_rejects_past_dates()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<CreateEventViewModel>();
        vm.Title = "Rencontre test";
        vm.Description = "Une description suffisamment longue pour passer.";
        vm.Category = "Culture";
        vm.Address = "Salle de test";
        vm.Date = DateTimeOffset.Now.AddDays(-3);

        Assert.False(vm.Validate());
        Assert.NotNull(vm.DateError);
    }

    [Fact]
    public async Task Create_post_requires_content()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<CreatePostViewModel>();
        vm.Content = "a";

        Assert.False(vm.Validate());
        Assert.NotNull(vm.ContentError);
    }
}
