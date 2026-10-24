using GreekSwitch.App.Policy;
using GreekSwitch.App.Settings;

namespace GreekSwitch.App.Tests;

public sealed class ManagedPolicyTests
{
    [Fact]
    public void No_policy_keys_means_nothing_is_set()
    {
        var policy = ManagedPolicy.Read(new FakeRegistry());

        policy.IsEmpty.Should().BeTrue();
        policy.Should().Be(ManagedPolicy.None);
    }

    [Fact]
    public void Values_are_read_from_either_key()
    {
        var registry = new FakeRegistry()
            .Set(PolicyScope.User, ManagedPolicy.EnabledValue, 0)
            .SetList(PolicyScope.User, ManagedPolicy.ExcludedProcessesValue, "outlook.exe", " ", "Teams.exe")
            .Set(PolicyScope.Machine, ManagedPolicy.DisableUndoLearningValue, 1)
            .Set(PolicyScope.Machine, ManagedPolicy.DisableLoanwordSuggestionsValue, 1);

        var policy = ManagedPolicy.Read(registry);

        policy.Enabled.Should().BeFalse();
        policy.ExcludedProcesses.Should().Equal("outlook.exe", "Teams.exe");
        policy.DisableUndoLearning.Should().BeTrue();
        policy.DisableLoanwordSuggestions.Should().BeTrue();
    }

    [Fact]
    public void Machine_policy_wins_over_user_policy()
    {
        var registry = new FakeRegistry()
            .Set(PolicyScope.Machine, ManagedPolicy.EnabledValue, 1)
            .Set(PolicyScope.User, ManagedPolicy.EnabledValue, 0)
            .SetList(PolicyScope.Machine, ManagedPolicy.ExcludedProcessesValue, "a.exe")
            .SetList(PolicyScope.User, ManagedPolicy.ExcludedProcessesValue, "b.exe")
            .Set(PolicyScope.Machine, ManagedPolicy.DisableUndoLearningValue, 0)
            .Set(PolicyScope.User, ManagedPolicy.DisableUndoLearningValue, 1);

        var policy = ManagedPolicy.Read(registry);

        policy.Enabled.Should().BeTrue();
        policy.ExcludedProcesses.Should().Equal("a.exe");
        policy.DisableUndoLearning.Should().BeFalse("HKLM says 0");
    }

    [Fact]
    public void Extra_always_excluded_apps_from_both_keys_are_combined()
    {
        var registry = new FakeRegistry()
            .SetList(PolicyScope.Machine, ManagedPolicy.AdditionalAlwaysExcludedValue, "Vault.exe")
            .SetList(PolicyScope.User, ManagedPolicy.AdditionalAlwaysExcludedValue, "vault.exe", "Secrets.exe");

        ManagedPolicy.Read(registry).AdditionalAlwaysExcluded.Should().Equal("Vault.exe", "Secrets.exe");
    }

    [Fact]
    public void Strings_are_accepted_for_flags_and_lists_and_wrong_types_are_ignored()
    {
        var registry = new FakeRegistry()
            .Set(PolicyScope.Machine, ManagedPolicy.EnabledValue, new byte[] { 1 }) // REG_BINARY: ignored
            .Set(PolicyScope.User, ManagedPolicy.EnabledValue, "0")
            .Set(PolicyScope.Machine, ManagedPolicy.ExcludedProcessesValue, "a.exe; b.exe")
            .Set(PolicyScope.Machine, ManagedPolicy.DisableUndoLearningValue, "yes"); // not a number: ignored

        var policy = ManagedPolicy.Read(registry);

        policy.Enabled.Should().BeFalse("the HKLM value has the wrong type, so HKCU applies");
        policy.ExcludedProcesses.Should().Equal("a.exe", "b.exe");
        policy.DisableUndoLearning.Should().BeFalse();
    }

    [Fact]
    public void An_empty_excluded_list_is_a_policy_too()
    {
        var registry = new FakeRegistry().Set(PolicyScope.Machine, ManagedPolicy.ExcludedProcessesValue, Array.Empty<string>());

        var policy = ManagedPolicy.Read(registry);

        policy.ExcludedProcesses.Should().BeEmpty();
        policy.EffectiveExcluded(new AppSettings()).Should().BeEmpty("IT replaced the user's list with an empty one");
    }

    [Fact]
    public void Policy_values_override_settings_without_changing_them()
    {
        var settings = new AppSettings { Enabled = true, ExcludedProcesses = ["mine.exe"], SuggestLoanwords = true };
        var policy = new ManagedPolicy { Enabled = false, ExcludedProcesses = ["theirs.exe"], DisableLoanwordSuggestions = true };

        policy.EffectiveEnabled(settings).Should().BeFalse();
        policy.EffectiveExcluded(settings).Should().Equal("theirs.exe");
        policy.EffectiveSuggestLoanwords(settings).Should().BeFalse();
        settings.Enabled.Should().BeTrue();
        settings.ExcludedProcesses.Should().Equal("mine.exe");

        ManagedPolicy.None.EffectiveSuggestLoanwords(settings).Should().BeTrue();
        ManagedPolicy.None.EffectiveSuggestLoanwords(new AppSettings { SuggestLoanwords = false }).Should().BeFalse();
    }

    [Fact]
    public void App_policy_applies_the_policy_on_top_of_settings()
    {
        var settings = new AppSettings { Enabled = true, ExcludedProcesses = ["mine.exe"] };
        var app = new AppPolicy();

        app.Apply(settings, new ManagedPolicy { Enabled = false, ExcludedProcesses = ["theirs.exe"], AdditionalAlwaysExcluded = ["Vault.exe"] });

        app.Enabled.Should().BeFalse();
        app.IsActive.Should().BeFalse();
        app.IsExcluded("THEIRS.EXE").Should().BeTrue();
        app.IsExcluded("mine.exe").Should().BeFalse();
        app.IsExcluded("vault.exe").Should().BeTrue();
        app.IsExcluded("KeePass.exe").Should().BeTrue("the built-in list always applies");

        app.Apply(settings, ManagedPolicy.None);

        app.Enabled.Should().BeTrue("without the policy the user's choice is back");
        app.IsExcluded("mine.exe").Should().BeTrue();
        app.IsExcluded("vault.exe").Should().BeFalse();
        app.IsExcluded("KeePass.exe").Should().BeTrue();
    }

    [Fact]
    public void Equal_policies_compare_equal_so_unchanged_policy_is_not_logged_again()
    {
        var a = new ManagedPolicy { ExcludedProcesses = ["a.exe"], AdditionalAlwaysExcluded = ["b.exe"] };
        var b = new ManagedPolicy { ExcludedProcesses = ["A.EXE"], AdditionalAlwaysExcluded = ["b.exe"] };

        a.Should().Be(b);
        a.Should().NotBe(ManagedPolicy.None);
    }

    private sealed class FakeRegistry : IPolicyRegistry
    {
        private readonly Dictionary<(PolicyScope, string), object> _values = [];

        public FakeRegistry Set(PolicyScope scope, string name, object value)
        {
            _values[(scope, name)] = value;
            return this;
        }

        /// <summary>A REG_MULTI_SZ value.</summary>
        public FakeRegistry SetList(PolicyScope scope, string name, params string[] lines) => Set(scope, name, lines);

        public object? GetValue(PolicyScope scope, string name) => _values.GetValueOrDefault((scope, name));
    }
}
