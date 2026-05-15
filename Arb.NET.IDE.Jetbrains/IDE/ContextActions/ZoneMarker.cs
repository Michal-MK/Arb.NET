using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ReSharper.Feature.Services;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.ReSharper.Psi.CSharp;
#if RIDER
using JetBrains.RdBackend.Common.Env;
#endif

namespace Arb.NET.IDE.Jetbrains.Shared.ContextActions;

[ZoneMarker]
public class ZoneMarker : IRequire<ICodeEditingZone>,
    IRequire<ILanguageCSharpZone>,
#if RIDER
    IRequire<IReSharperHostCoreSharedFeatureZone>,
#endif
    IRequire<PsiFeaturesImplZone>;
