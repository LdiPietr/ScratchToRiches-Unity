# Scratch to Riches — architecture and refactoring baseline

Scope: preserve gameplay/economy while preparing visual design and UX.
The catalog, ticket gates, probability weights, existing save keys, scene,
original MonoScript GUIDs and serialized references remain unchanged.

## Responsibilities and patterns

- **Facade / controller**: existing MonoBehaviours retain serialized
  references and public APIs. No global singleton.
- **Domain policy / functional core**: ScratchRewardPolicy owns payout math;
  economy manager owns wallet mutation, settlement guard and persistence.
  ScratchMatchEvaluator owns matching; ScratchSymbolManager adapts revealed
  cells to symbol IDs. RNG stays in the original generation path.
- **Single purchase policy**: ScratchPurchasePolicy returns explicit reasons
  (edition, implementation, max level, chapter, prerequisites, balance).
  Both the actual authorization and details panel consume the same result;
  the hold input still delegates to the actual purchase method.
- **Dependency injection via parameters**: payout receives a skill-level query,
  matching receives revealed IDs and labels rather than scene objects.
- **View adapter / presentation**: ScratchSkillMapNodeView owns per-node
  appearance; ScratchSkillTreeUI orchestrates purchases and navigation.
- **Observer**: existing Changed event subscriptions and notifications are
  preserved. Editor-only telemetry stays separate.
- **Finite state machine**: round/presentation phases and transitions
  preserved; ticket and shop animation responsibilities separated.
- **Composition before inheritance**: extracted services contain rules;
  partial files group existing serialized controllers and QA by responsibility.
  Partial classes alone are not a design pattern or a domain boundary.

## File map

- ScratchGameManager.cs: ScratchGameManager.cs, ScratchGameManager.Finale.cs, ScratchGameManager.Tickets.cs, ScratchGameManager.Hud.cs
- ScratchSkillTreeUI.cs: ScratchSkillTreeUI.cs, ScratchSkillTreeUI.Details.cs, ScratchSkillTreeUI.Rendering.cs
- ScratchSkillTree.cs: ScratchSkillTree.cs, ScratchSkillTree.LegacyCatalog.cs, ScratchSkillTree.Powers.cs, ScratchSkillTree.Reset.cs
- ScratchCell.cs: ScratchCell.cs, ScratchCell.Gestures.cs, ScratchCell.Mask.cs
- ScratchFlowPresentation.cs: ScratchFlowPresentation.cs, ScratchFlowPresentation.TicketAnimation.cs, ScratchFlowPresentation.ShopAnimation.cs, ScratchFlowPresentation.Hud.cs
- Editor/ScratchAutoplayWindow.cs: ScratchAutoplayWindow.cs, ScratchAutoplayWindow.Controls.cs, ScratchAutoplayWindow.Session.cs, ScratchAutoplayWindow.Selection.cs, ScratchAutoplayWindow.Scratch.cs, ScratchAutoplayWindow.Progression.cs, ScratchAutoplayWindow.Purchases.cs, ScratchAutoplayWindow.Farming.cs, ScratchAutoplayWindow.Reports.cs
- Editor/ScratchManualPlaytestRecorderWindow.cs: ScratchManualPlaytestRecorderWindow.cs, ScratchManualPlaytestRecorderWindow.Session.cs, ScratchManualPlaytestRecorderWindow.Capture.cs, ScratchManualPlaytestRecorderWindow.Reports.cs

## Verification

Run `dotnet run --project QA/DomainChecks/DomainChecks.csproj -c Release` for
headless tests of the actual C# match/reward/purchase policies. The runner
links their original source and stable value types, without Unity stubs.
It enumerates every symbol sequence of up to five picks, compares 50,000
reward cases with the original payout formula, and checks all 128 purchase
condition combinations. CI runs these tests in addition to existing Python
checks. This does not compile the full Unity project or run its scene.


The existing static QA reads complete partial classes and retains progression,
scene and playtest checks. Unity Scene Audit exercises the extracted reward and
matching methods with deterministic cases. Compile, audit and play mode remain
necessary before claiming a runtime pass. Do not mechanically introduce every
GoF pattern: add strategies, commands, factories or repositories when they
address a genuine variation or testing boundary.
