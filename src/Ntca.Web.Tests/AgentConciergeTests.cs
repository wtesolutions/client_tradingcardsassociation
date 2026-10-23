using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class AgentConciergeTests
{
    private static List<AgentUaRule> Catalog() =>
    [
        new() { Token = "ChatGPT-User", AssistantName = "ChatGPT", Role = "USER_ASSISTANT", IsEnabled = true, SortOrder = 10 },
        new() { Token = "Claude-User", AssistantName = "Claude", Role = "USER_ASSISTANT", IsEnabled = true, SortOrder = 20 },
        new() { Token = "Grok-User", AssistantName = "Grok", Role = "USER_ASSISTANT", IsEnabled = true, SortOrder = 50 },
        new() { Token = "OAI-SearchBot", AssistantName = "ChatGPT", Role = "SEARCH", IsEnabled = true, SortOrder = 60 },
        new() { Token = "GPTBot", AssistantName = "ChatGPT", Role = "TRAINING", IsEnabled = true, SortOrder = 110 },
        new() { Token = "ClaudeBot", AssistantName = "Claude", Role = "TRAINING", IsEnabled = true, SortOrder = 120 }
    ];

    [Theory]
    [InlineData("Mozilla/5.0 ChatGPT-User", "ChatGPT", "USER_ASSISTANT")]
    [InlineData("Claude-User/1.0", "Claude", "USER_ASSISTANT")]
    [InlineData("Grok-User", "Grok", "USER_ASSISTANT")]
    [InlineData("Mozilla/5.0 compatible; GPTBot/1.2", "ChatGPT", "TRAINING")]
    [InlineData("ClaudeBot", "Claude", "TRAINING")]
    [InlineData("OAI-SearchBot", "ChatGPT", "SEARCH")]
    public void Classifier_matches_catalog_tokens(string ua, string assistant, string role)
    {
        var v = AgentClassifier.Classify(ua, Catalog());
        Assert.True(v.IsAgent);
        Assert.Equal(assistant, v.AssistantName);
        Assert.Equal(role, v.Role);
        Assert.False(v.IsVerified);
    }

    [Fact]
    public void Classifier_ignores_ordinary_browsers()
    {
        var v = AgentClassifier.Classify("Mozilla/5.0 (Macintosh) Chrome/129.0", Catalog());
        Assert.False(v.IsAgent);
        Assert.Equal("", v.AssistantName);
    }

    [Fact]
    public void WebBotAuth_marks_structural_signature_verified()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["Signature-Agent"] = "https://chatgpt.com";
        http.Request.Headers["Signature-Input"] = "sig1=(\"@authority\");created=1710000000;keyid=\"k1\"";
        http.Request.Headers["Signature"] = "sig1=:abc:";
        var (presented, verified, agent) = WebBotAuth.Inspect(http.Request);
        Assert.True(presented);
        Assert.True(verified);
        Assert.Equal("https://chatgpt.com", agent);
    }

    [Fact]
    public void Privacy_strips_forbidden_keys_and_redacts_question()
    {
        var json = JsonDocument.Parse("""
            {
              "assistant": "ChatGPT",
              "humanRole": "collector",
              "name": "Jane Doe",
              "email": "jane@example.com",
              "phone": "919-555-0100",
              "location": { "city": "Raleigh", "state": "North Carolina", "zip": "27601-1234" },
              "interests": ["Pokémon", "sports cards"],
              "question": "Call me at 919-555-0199 or jane@example.com about shops"
            }
            """).RootElement;
        var ask = new AgentAsk
        {
            Assistant = "ChatGPT",
            HumanRole = "collector",
            Location = new AgentLocation { City = "Raleigh", State = "North Carolina", Zip = "27601-1234" },
            Interests = ["Pokémon", "sports cards"],
            Question = "Call me at 919-555-0199 or jane@example.com about shops"
        };
        var result = AgentPrivacy.Sanitize(json, ask);
        Assert.True(result.PiiStripped);
        Assert.Contains(result.Warnings, w => w.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("collector", result.HumanRole);
        Assert.Equal("NC", result.StateCode);
        Assert.Equal("276", result.Zip3);
        Assert.Equal(new[] { "pokemon", "sports" }, result.Interests);
        Assert.Contains("[redacted-phone]", result.Question);
        Assert.Contains("[redacted-email]", result.Question);
        Assert.DoesNotContain("Jane", result.Question ?? "");
        Assert.DoesNotContain("919-555-0199", result.Question ?? "");
    }

    [Fact]
    public void Privacy_rejects_unknown_roles_and_empty_zip()
    {
        var ask = new AgentAsk { HumanRole = "wizard", Location = new AgentLocation { Zip = "12" } };
        var result = AgentPrivacy.Sanitize(default, ask);
        Assert.Null(result.HumanRole);
        Assert.Null(result.Zip3);
        Assert.False(result.PiiStripped);
    }
}
