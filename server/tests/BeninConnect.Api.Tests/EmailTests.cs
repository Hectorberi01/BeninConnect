using System.Net;
using System.Text.Json;
using BeninConnect.Host;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Infrastructure;
using BeninConnect.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BeninConnect.Api.Tests;

public sealed class EmailTests
{
    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent("{\"id\":\"e1\"}") };
        }
    }

    private static (ResendEmailSender, CapturingHandler) Sender(HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new CapturingHandler(status);
        var http = new HttpClient(handler) { BaseAddress = new Uri(ResendEmailSender.BaseAddress) };
        var settings = new EmailSettings { Provider = "resend", ResendApiKey = "re_test", From = "no-reply@monbenin.com", FromName = "MonBénin" };
        return (new ResendEmailSender(http, settings, NullLogger<ResendEmailSender>.Instance), handler);
    }

    [Fact]
    public async Task Resend_receives_the_verification_email()
    {
        var (sender, handler) = Sender();
        await sender.SendAsync("ami@exemple.test", "123456 est votre code", "Bonjour,\nVotre code : 123456 <test>", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.resend.com/emails", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer re_test", handler.Request.Headers.Authorization!.ToString());

        var json = JsonDocument.Parse(handler.Body).RootElement;
        Assert.Equal("MonBénin <no-reply@monbenin.com>", json.GetProperty("from").GetString());
        Assert.Equal("ami@exemple.test", json.GetProperty("to")[0].GetString());
        Assert.Equal("123456 est votre code", json.GetProperty("subject").GetString());
        Assert.Contains("Votre code : 123456", json.GetProperty("text").GetString());
        Assert.Contains("&lt;test&gt;", json.GetProperty("html").GetString()); // texte échappé dans le HTML
    }

    [Fact]
    public async Task A_refused_email_does_not_break_the_request()
    {
        var (sender, _) = Sender(HttpStatusCode.UnprocessableEntity);
        await sender.SendAsync("ami@exemple.test", "Sujet", "Texte", CancellationToken.None); // ne lève pas
    }

    [Fact]
    public void Production_requires_the_resend_api_key()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:SeedDemoData"] = "false",
            ["ConnectionStrings:Database"] = "Host=db.exemple.test;Database=b;Username=u;Password=p;SSL Mode=Require"
        }).Build();
        var jwt = new JwtOptions { SigningKey = new string('k', 48) };

        var error = Assert.Throws<InvalidOperationException>(() =>
            Production.Validate(config, new Env(), jwt, new AuthSettings(), new EmailSettings { Provider = "resend", From = "no-reply@monbenin.com" }));
        Assert.Contains("Email__ResendApiKey", error.Message);

        Production.Validate(config, new Env(), jwt, new AuthSettings(),
            new EmailSettings { Provider = "resend", From = "no-reply@monbenin.com", ResendApiKey = "re_test" });
    }

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
