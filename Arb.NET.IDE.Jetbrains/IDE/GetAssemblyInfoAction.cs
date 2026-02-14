using System.Text;
using Arb.NET.IDE.Jetbrains.Common;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Util;

namespace Arb.NET.IDE.Jetbrains.Shared;

[Action(ResourceType: typeof(CommonResources), TextResourceName: nameof(CommonResources.AssemblyActionName))]
public class GetAssemblyInfoAction : IExecutableAction
{
    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate) => true;

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
        // ReSharper disable once RedundantNameQualifier
        var asm = typeof(Arb.NET.ArbParser).Assembly;
        var name = asm.GetName();

        var sb = new StringBuilder();
        sb.AppendLine($"Name:      {name.Name}");
        sb.AppendLine($"Version:   {name.Version}");
        sb.AppendLine($"Location:  {asm.Location}");
        sb.AppendLine($"Types:     {asm.GetTypes().Length}");

        MessageBox.ShowInfo(sb.ToString(), "Arb.NET Assembly Info");
    }
}