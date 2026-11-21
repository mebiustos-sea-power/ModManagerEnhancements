using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using SeapowerUI.ViewModels;

namespace ModManagerEnhancements;

// The game's message-box model is internal; keep that reflection boundary in one place.
internal sealed class NativeMessageBox
{
    private static readonly Type ModelType = typeof(ModMenuViewModel).Assembly.GetType("SeapowerUI.ViewModels.MessageBoxViewModel", true)!;
    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties = new[]
    {
        "Header", "Message", "MessageBoxType", "CancelCommandLabel", "AcknowledgeCommandLabel",
        "CancelCommand", "AcknowledgeCommand", "CancelAvailable", "AcknowledgeAvailable", "MiddleAvailable"
    }.ToDictionary(name => name, name => ModelType.GetProperty(name)
        ?? throw new MissingMemberException(ModelType.FullName, name));

    internal object Model { get; } = Activator.CreateInstance(ModelType)!;
    private T Get<T>(string name) => (T)Properties[name].GetValue(Model)!;
    private void Set(string name, object value) => Properties[name].SetValue(Model, value);
    internal string Header { get => Get<string>("Header"); set => Set("Header", value); }
    internal string Message { get => Get<string>("Message"); set => Set("Message", value); }
    internal string Kind { get => Properties["MessageBoxType"].GetValue(Model)!.ToString()!;
        set => Set("MessageBoxType", Enum.Parse(Properties["MessageBoxType"].PropertyType, value)); }
    internal string CancelCommandLabel { get => Get<string>("CancelCommandLabel"); set => Set("CancelCommandLabel", value); }
    internal string AcknowledgeCommandLabel { get => Get<string>("AcknowledgeCommandLabel"); set => Set("AcknowledgeCommandLabel", value); }
    internal ICommand CancelCommand { get => Get<ICommand>("CancelCommand"); set => Set("CancelCommand", value); }
    internal ICommand AcknowledgeCommand { get => Get<ICommand>("AcknowledgeCommand"); set => Set("AcknowledgeCommand", value); }
    internal bool CancelAvailable => Get<bool>("CancelAvailable");
    internal bool AcknowledgeAvailable => Get<bool>("AcknowledgeAvailable");
    internal bool MiddleAvailable => Get<bool>("MiddleAvailable");
}
