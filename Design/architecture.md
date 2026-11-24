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

The existing static QA reads complete partial classes and retains progression,
scene and playtest checks. Unity Scene Audit exercises the extracted reward and
matching methods with deterministic cases. Compile, audit and play mode remain
necessary before claiming a runtime pass. Do not mechanically introduce every
GoF pattern: add strategies, commands, factories or repositories when they
address a genuine variation or testing boundary.
