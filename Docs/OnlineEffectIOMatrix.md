# OnlineEffectIOMatrix

작성일: 2026-07-07

목적: 온라인 effectRef 구현 전에 Host input, Host calculation, Result output, Delta type, private info 정책을 effectRef 단위로 고정한다. 온라인 구현은 새 룰을 만들지 않고, 로컬 `EffectManager` / `BattleManager` / 관련 서비스의 결과를 Host에서 동일하게 재현한 뒤 `BattleActionResult`와 Delta로 직렬화한다.

## 공통 원칙

- `actor`: 액션을 낸 플레이어. Host에서는 Host local `My` 또는 remote `Enemy`일 수 있다.
- `source`: 콘텐츠는 손패/필드의 실제 source card, 캐릭터 액티브는 source slot의 character, 아이돌 액티브는 반드시 `action.actor`의 idol.
- `slot.owner`: 방송 플랫폼 원소유자.
- `characterOwner`: 캐릭터 카드 소유자/조종자. 상대 방송 슬롯 위로 이동해도 바뀌지 않는다.
- Client 수신 시 Host 기준 `My/Enemy`를 그대로 적용하지 않는다. result actor, currentTurnPlayer, slot id owner, delta owner, selection request owner를 local perspective로 remap해야 한다.
- 손패 `PostCollab` 콘텐츠는 자동 발동이 아니다. Host가 후보 감지/사용 가능 상태를 만들고, 사용 액션이 들어왔을 때 로컬 조건으로 검증한다.
- `BroadcastAlways` / `Passive` 중 이미 시청자, HP, 합방 텐션 계산에 포함되는 효과는 중복 Delta를 만들지 않는다. Host 계산식 snapshot/result에 반영한다.
- 비공개 zone인 deck/hand 조작은 owner 전용 payload와 opponent sanitize payload를 분리한다.

## 약어

- Public input: slot id, card instance id가 공개 상태인 필드/휴식존/공개 카드 선택.
- Private input: deck/hand 순서, 손패 카드 선택, owner-only deck candidate id.
- Deltas: `Viewer`, `FieldStat`, `CardZoneMove`, `SelectionRequest`, `PrivateSelectionPayload`, `DeckOrder`, `Status`, `ActionState`, `Log/Animation`.
- Consume: 행동권 소모. `Yes`는 성공 적용 시 소모, `No`는 계산 포함/강제 트리거/패시브, `OnSuccess`는 취소/실패 시 미소모.
- Cancel: 사용자가 취소할 수 있는 시점. `None`, `BeforeTarget`, `DuringSelection`, `FallbackBasic`, `NoAction`.
- Priority: `A` 바로 구현 가능, `B` Selection Flow 필요, `C` Private Payload 필요, `D` Persistent Status 필요, `E` Host 계산식 통합 필요, `F` 보류/트리거 설계 필요.

## 요약

- 전체 non-empty effectRef: 44
- 데이터상 빈 ref: 1 (`BRST-STL001 저스트 채팅`) - 매트릭스 제외

### Primary Classification Counts

| Classification | Count |
|---|---:|
| SimplePublicDelta | 3 |
| AutoPublicMultiTargetDelta | 1 |
| PublicSelection | 9 |
| PrivateSelection | 8 |
| PersistentStatus | 10 |
| IncludedInHostCalculation | 9 |
| ManualTriggerCandidate | 4 |
| Deferred/NeedsDesign | 0 |

Secondary flags:

| Flag | Count | effectRef |
|---|---:|---|
| HostRng / deck order authority | 2 | `content.redrawIfBehindAndUniverseOnly`, `content.returnUpToNFromRestToDeck` |
| High owner/remap risk | 11 | see Owner/Remap Risk section |

### Priority Counts

| Priority | Count |
|---|---:|
| A | 4 |
| B | 9 |
| C | 8 |
| D | 10 |
| E | 9 |
| F | 4 |

### Public Selection Subtypes

`PublicSelection` must be split by local UI policy before online implementation. Do not assume every public target/filter effect creates a `SelectionRequestDelta`.

| Subtype | Local behavior | Online handling | Examples |
|---|---|---|---|
| PublicSelectionRequired | A target selection UI is required even if there is only one candidate. | Host creates `SelectionRequestDelta`; selected player sends `SelectEffectTargetAction`; cancel policy follows local UI. | `character.active.modifyTaggedOnBoard` |
| PublicSelectionAutoSingle | Candidate count 1 resolves automatically; candidate count 2+ opens selection UI. | Host auto-resolves one candidate; otherwise creates `SelectionRequestDelta`. | `idol.active.fullHealOneControlled` |
| PublicAutoResolveMultiTarget | No selection UI; all valid public candidates are applied automatically. | Host creates no `SelectionRequestDelta`; Host emits all public result Deltas directly. | `character.active.adjacentHpDownAndTensionUpForTag` |

## Matrix

| effectRef | Type / timing | Local 처리 위치 | Actor / source 기준 | Host input / selection / private | Host result output | Delta types | Consume / cancel | Classification / priority | owner/remap 주의 |
|---|---|---|---|---|---|---|---|---|---|
| `broadcast.always.disableIdolActiveAndLockMoveOnEnter` | BroadcastAlways / Always | `BattleManager.IsIdolActiveDisabledByBroadcastFromExternal`, `ApplyBroadcastEnterEffectsFromExternal` | occupant `characterOwner`; broadcast by `slot.owner` | No user input | Idol active disable and movement lock in Host validation/state | Status, ActionState, Log | No / None | PersistentStatus / D | Must check occupant owner, not slot owner, for idol active disable target. |
| `broadcast.always.gainViewersWhenOccupantLeaves` | BroadcastAlways / Always leave trigger | `BattleManager.ApplyBroadcastLeaveEffectsFromExternal` | leaving character `characterOwner`; broadcast `slot.owner` | Leave event from Host field move/rest resolution | Viewer gain when occupant leaves | Viewer, Log | No / None | ManualTriggerCandidate / F | Trigger from original source slot before `ClearCharacterCard`; do not use destination slot owner. |
| `broadcast.always.noFaceDownSummonAndDisablePreCollabEffects` | BroadcastAlways / Always | `SummonManager`, `EffectManager.ShouldSkipEffectCandidateDueToCollabSilence`, `BattleManager.HasBroadcastEffectBoolParam` | slot broadcast rule; affected occupant by `characterOwner` | No user input | Face-down summon forbid; pre-collab effect silence in validations | Status, Log | No / None | PersistentStatus / D | Summon restriction is `slot.owner`; pre-collab silence is occupant `characterOwner`. |
| `broadcast.always.prepViewersAndHealBonus` | BroadcastAlways / Always | `BattleManager.CalculateBroadcastHealBonus`, prep viewer calculation | slot broadcast rule; prep owner from actor/current prep owner | No user input | Included in prep viewer/heal calculation | BattleCountSnapshot, Log | No / None | IncludedInHostCalculation / E | No extra Delta if Host snapshot already includes value. |
| `broadcast.always.prepViewersAndOccupantHpDelta` | BroadcastAlways / Always | `BattleManager` broadcast HP max/stat refresh paths | occupant `characterOwner`; platform `slot.owner` | No user input | Max HP modifier and prep viewer modifier in Host calculation | FieldStat if state refresh needed, BattleCountSnapshot | No / None | IncludedInHostCalculation / E | HP modifier follows occupant, not platform owner. |
| `broadcast.always.taggedOccupantPrepViewersBonus` | BroadcastAlways / Always | `BattleManager.CalculateTaggedOccupantPrepViewerBonus` | prep owner and tagged occupant `characterOwner` | No user input | Prep viewer bonus in Host calculation | BattleCountSnapshot | No / None | IncludedInHostCalculation / E | A character on opponent platform still contributes by `characterOwner` where local rule allows. |
| `character.active.adjacentHpDownAndTensionUpForTag` | CharacterActive | `EffectManager.ResolveModifyCharacterStatsEffectWithResult`, `BuildAdjacentHpDownAndTensionUpRequest`, `EffectStatService.ModifyCharacterStats`, `EffectTargetingService.AdjacentToSource` | source slot `characterOwner`; source character active | Public adjacent field filter; no user selection (`requireTargetSelection=false`, `maxTargets=0`) | HP down and tension up for all adjacent valid candidates; HP 0 may move target to rest and merge supported OnRest simple Deltas | FieldStat, Viewer if cost, ActionState, CardZoneMove, supported OnRest simple Delta, Log | Yes / None | AutoPublicMultiTargetDelta / A | Source validation uses `characterOwner == actor`; target ownership follows local filter. `slot.owner` is used for adjacency only, not card ownership. Cross-owner adjacency follows local `AdjacentToSource`; HP 0 rest owner is `characterOwner`. |
| `character.active.adjacentOppCollabTensionDeltaThisTurn` | CharacterActive | `EffectManager.ResolveModifyCharacterStatsEffectWithResult`, status expiry | source slot `characterOwner` | Public field/context target | Temporary adjacent opponent collab tension modifier | SelectionRequest, Status, ActionState, Log | Yes / DuringSelection | PersistentStatus / D | Status target should be characterOwner/slot id pair; expires by Host turn count. |
| `character.active.discardOneThenFetchContentByTagFromDeck` | CharacterActive | `EffectManager.ResolveDiscardOneThenFetchContentByTagFromDeckEffect` | source slot `characterOwner`; hand/deck owner is actor | Private discard from hand, private deck content candidate | Chosen hand card to rest, chosen deck content to hand | SelectionRequest, PrivateSelectionPayload, CardZoneMove, CardDraw, DeckOrder, ActionState, Log | OnSuccess / DuringSelection | PrivateSelection / C | Owner-only selected card ids; opponent sees counts/public reveals only if local rule reveals. |
| `character.active.forceBattleTargetAnywhere` | CharacterActive | `EffectManager.ResolveForceBattleTargetAnywhereEffect` | source slot `characterOwner` | Public field target | Force/override battle target status until resolved | SelectionRequest, Status, ActionState, Log | Yes / DuringSelection | PublicSelection / B | Forced attacker/defender owner must use target `characterOwner`. |
| `character.active.modifyTaggedOnBoard` | CharacterActive | `EffectManager.ResolveModifyCharacterStatsEffectWithResult`; online `CreateCharacterActiveModifyTaggedOnBoardSelectionResult` | source slot `characterOwner` | Public tagged field character target | Permanent max HP/tension stat changes | SelectionRequest, FieldStat, Viewer, ActionState, Log | Yes / DuringSelection | PublicSelection / B | Already online path; candidate owner uses `characterOwner`, not slot owner. |
| `character.active.peekTopAndTakeTaggedContents` | CharacterActive | `EffectManager.ResolvePeekTopSelectToHandEffect`, `EffectDeckPeekService` | source slot `characterOwner`; deck owner actor | Private deck top peek and card option | Take tagged contents to hand, bottom/return rest by local rule | SelectionRequest, PrivateSelectionPayload, CardDraw, CardZoneMove, DeckOrder, Log | OnSuccess / DuringSelection | PrivateSelection / C | Only actor can see peeked card ids; opponent gets sanitized deck count/order delta. |
| `character.fetchCardsToHandByTags` | OnAppear or OnRest | `EffectManager.ResolveSearchDeckSelectToHandEffect` | effect owner is character `characterOwner` | Private deck candidate selection by tag | Selected cards move deck to hand | SelectionRequest, PrivateSelectionPayload, CardDraw, DeckOrder, Log | No for mandatory trigger; OnSuccess for manual | PrivateSelection / C | Same ref appears on `OnAppear` and `Rest`; source timing must be preserved. |
| `character.onAppear.adjacentOppCollabTensionDeltaThisTurn` | OnAppear | `EffectManager.ResolveModifyCharacterStatsEffectWithResult` | appeared character `characterOwner` | Trigger context from summon/appearance | Temporary adjacent opponent collab tension modifier | Status, Log | No / None | PersistentStatus / D | Trigger only after Host-applied appearance; source slot id must be remapped. |
| `character.onAppear.callFromRestByTagToEmptyPlatforms` | OnAppear | `EffectManager.ResolveCallFromRestByTagToEmptyPlatformsEffect` | appeared character `characterOwner` | Public/owner rest-zone candidate and empty platform target | Rest character to field, possible OnAppear chain | SelectionRequest, CardZoneMove, ActionState if chain consumes, Log | No for trigger / DuringSelection | PublicSelection / B | Rest zone is public in current local UI; destination slot owner is platform, characterOwner remains owner. |
| `character.passive.adjacentCollabTensionDeltaForTag` | Passive | `BattleManager` / `EffectManager` collab tension calculation | participant `characterOwner` and adjacency | No user input | Included in Host StartCollab calculation | StartCollabResult stats, Log | No / None | IncludedInHostCalculation / E | No duplicate StatusDelta; Host collab result must be authoritative. |
| `character.passive.doubleStepMoveNoJump` | Passive | `MovementManager` move candidate/range validation | moving character `characterOwner` | No user input | Alters legal move candidates | Status or included movement validation | No / None | PersistentStatus / D | Movement legality follows characterOwner even on opponent platform. |
| `character.passive.reduceOwnerPrepViewers` | Passive | `BattleManager.CalculatePrepViewerPassiveBonus` | passive character `characterOwner` | No user input | Prep viewer reduction in Host calculation | BattleCountSnapshot | No / None | IncludedInHostCalculation / E | Do not apply viewer delta separately at prep if snapshot already includes it. |
| `character.passive.viewersBonusIfAdjacentToTag` | Passive | `BattleManager.CalculatePrepViewerPassiveBonus` | passive character `characterOwner` | No user input | Prep viewer bonus in Host calculation | BattleCountSnapshot | No / None | IncludedInHostCalculation / E | Adjacent owned/tagged check must use characterOwner. |
| `character.rest.gainViewers` | OnRest | `EffectManager.TryResolveEffect`, `ModifyViewers` | rested character `characterOwner` | Rest trigger from Host KO/rest move | Viewer gain | Viewer, Log | No / None | SimplePublicDelta / A | Trigger owner is card owner before removing from field. |
| `character.rest.loseViewers` | OnRest | `EffectManager.TryResolveEffect`, `ModifyViewers` | rested character `characterOwner` | Rest trigger from Host KO/rest move | Viewer loss | Viewer, Log | No / None | SimplePublicDelta / A | Same as gain; amount sign is local rule. |
| `character.rest.reduceOpponentCollabTensionOnCollab` | OnRest | `EffectManager.CanActivateEffect`, collab context checks | rested character `characterOwner`, only when collab caused rest | Host collab/rest context | Temporary/one-result collab tension reduction | Status or included StartCollabResult, Log | No / None | PersistentStatus / D | Must not fire for ordinary rest; requires collab context. |
| `content.collabClicheSpendBuffRefund` | PreCollab | `EffectManager.ResolveCollabClicheSpendBuffRefundEffect`, `CanActivateCollabClicheSpendBuffRefund` | hand content owner actor; collab context participant | Candidate detection, user use decision in PreCollab window | Spend/refund viewers and buff for collab | Viewer, FieldStat/Status, ActionState, Log | OnSuccess / BeforeTarget | ManualTriggerCandidate / F | Hand Post/PreCollab is candidate, not automatic. Actor is card owner using from hand. |
| `content.drawThenDiscard` | Content | `EffectManager.ResolveDrawThenDiscardEffect` | hand content owner actor | Private draw then private discard choice | Drawn card(s), discarded card(s) | PrivateSelectionPayload, CardDraw, CardZoneMove, DeckOrder, Viewer, ActionState, Log | OnSuccess / DuringSelection | PrivateSelection / C | Drawn card identity owner-only; discard may become public in rest. |
| `content.forbidOpponentAttackUntilNextTurn` | PostCollab | `EffectManager.ResolveForbidOpponentAttackUntilNextTurnEffect` | hand content owner actor; surviving opponent collab participant | Candidate detection/use in PostCollab window | Forbid target opponent attack until next turn | Status, ActionState, Log | OnSuccess / NoAction | PersistentStatus / D | Target is surviving opponent participant `characterOwner`, not platform owner. |
| `content.forceOpponentFlipOrSack` | Content | `EffectManager.ResolveForceOpponentFlipOrSackEffect` | hand content owner actor | Public opponent face-down field slot selection | Flip target or send/sack per local rule | SelectionRequest, CardZoneMove, CardReveal, Viewer, ActionState, Log | OnSuccess / DuringSelection | PublicSelection / B | Face-down card id may be hidden until flip; public target is slot id. |
| `content.forceOpponentSummonOrSackFromHand` | Content | `EffectManager.ResolveForceOpponentSummonOrSackFromHandEffect` | hand content owner actor; opponent hand owner | Opponent private hand choice, maybe public destination | Opponent summons selected card or sends to rest | SelectionRequest, PrivateSelectionPayload, CardZoneMove, CardReveal, ActionState, Log | OnSuccess / DuringSelection | PrivateSelection / C | Opponent hand payload must be sent only to opponent; actor sees sanitized result. |
| `content.invertNegativeAmountForTagThisTurn` | Content | `EffectManager.RegisterNegativeAmountInvertThisTurn`, `BattleManager` amount application | hand content owner actor | No target if local ref has no selection | Register temporary amount inversion | Status, Viewer, ActionState, Log | Yes / NoAction | PersistentStatus / D | Status owner is actor; later amount inversion must run in Host calculation only once. |
| `content.lasting.buffTagTensionAndHp` | Content / lasting install | `EffectManager.TryResolveEffect`, `BattleManager.GetInstalledContentCharacterStatModifier` | hand content owner actor; installed content slot owner/contentOwner | Placement/install input per local content flow | Installed lasting content affects HP/tension in collab calculation | Status, FieldContent, CardZoneMove, FieldStat, ActionState, Log | Yes / BeforeTarget | PersistentStatus / D | Once installed, later buffs are included in Host calculation; avoid duplicate per-collab Delta. |
| `content.lockBroadcastIdNoMoveNoKOUntilNextEnd` | Content | `EffectManager.ResolveLockBroadcastIdNoMoveNoKOUntilNextEndEffect` | hand content owner actor; target broadcast id/slot | Public broadcast/slot target per local rule | Movement/KO lock until next end | Status, CardZoneMove, Viewer, ActionState, Log | OnSuccess / DuringSelection | PersistentStatus / D | Lock follows target slot/broadcast; occupant owner may differ. |
| `content.moveOwnCharToEmptyOrBattleIfTagged` | Content | `EffectManager.ResolveMoveOwnCharToEmptyOrBattleIfTaggedEffect`; online `CreateMoveTaggedCharacterFinalResult` | hand content owner actor; source characterOwner actor | Public own tagged character, then public empty/collab destination | Move character or start effect collab; cost paid | SelectionRequest, CardZoneMove, Viewer, ActionState, Log | OnSuccess / DuringSelection | PublicSelection / B | Highest risk: destination `slot.owner` must not overwrite moving `characterOwner`. |
| `content.peekTopAndTakeTaggedCharacterOrBottom` | Content | `EffectManager.ResolvePeekTopSelectToHandEffect`, `EffectDeckPeekService` | hand content owner actor | Private deck top peek/choice | Take tagged character to hand or bottom card | SelectionRequest, PrivateSelectionPayload, CardDraw, DeckOrder, Viewer, ActionState, Log | OnSuccess / DuringSelection | PrivateSelection / C | Owner-only peek result; opponent sees count/order-safe delta only. |
| `content.postCollabHealOwnParticipant` | PostCollab | `EffectManager.HealOwnCollabParticipant`, `CanActivateEffect` | hand content owner actor; own surviving collab participant | Candidate detection/use in PostCollab window | Heal own participant | FieldStat, ActionState, Log | OnSuccess / NoAction | ManualTriggerCandidate / F | Trigger target by `characterOwner`; no manual target if local picks own surviving participant. |
| `content.postCollabTabiBoostAndRebattle` | PostCollab | `EffectManager.ResolvePostCollabTabiBoostAndRebattleEffect`, `CanActivatePostCollabTabiBoostAndRebattle` | hand content owner actor; collab context | Candidate detection/use, public #뿡댕이 cost if local requires | Boost Tabi and rebattle/follow-up collab | SelectionRequest, FieldStat, CardZoneMove, Viewer, Status, ActionState, Log | OnSuccess / DuringSelection | ManualTriggerCandidate / F | PostCollab candidate only; collab participant ownership and rebattle target remap are high risk. |
| `content.redrawIfBehindAndUniverseOnly` | Content | `EffectManager.RedrawIfBehindAndUniverseOnly`, `CanActivateRedrawIfBehindAndUniverseOnly` | hand content owner actor | Private hand/deck state; no opponent card identities | Redraw/shuffle/reorder according to local rule | PrivateSelectionPayload, CardDraw, CardZoneMove, DeckOrder, Viewer, ActionState, Log | OnSuccess / NoAction | PrivateSelection / C | Host owns deck order authority; all drawn card ids owner-only. |
| `content.removeAllLastingContentsOnBoard` | Content | `EffectManager.RemoveAllLastingContentsOnBoard`; online `ResolveRemoveAllLastingContents` | hand content owner actor | No target | Remove all lasting contents on board to rest | FieldContent, CardZoneMove, Viewer, ActionState, Log | Yes / NoAction | SimplePublicDelta / A | Field content owner, not slot owner, determines rest zone owner. |
| `content.returnUpToNFromRestToDeck` | Content | `EffectManager.ResolveReturnUpToNFromRestToDeckEffect` | hand content owner actor | Public rest-zone card multi-select | Rest cards move to deck, maybe shuffle/top/bottom per params | SelectionRequest, CardZoneMove, DeckOrder, Viewer, ActionState, Log | OnSuccess / DuringSelection | PublicSelection / B | Rest zone card owner controls deck owner; Host must decide deck order/shuffle. |
| `content.silenceCharacterCollabThisTurn` | Content | `EffectManager.TryResolveEffect`; online `TryStartSilenceCharacterCollabThisTurnFromExternal` | hand content owner actor | Public face-up field character target | Silence collab effects this turn; source content to rest | SelectionRequest, Status, CardZoneMove, Viewer, ActionState, Log | OnSuccess / DuringSelection | PublicSelection / B | Status target is selected character slot/card; characterOwner may differ from slot owner. |
| `idol.active.callFromRestByTagThenDonateViewers` | IdolActive | `EffectManager.ResolveCallFromRestByTagThenDonateViewersEffect` | `action.actor` idol | Public/owner rest-zone candidate and destination | Call tagged rest character to field, donate viewers to opponent | SelectionRequest, CardZoneMove, Viewer, ActionState, Log | OnSuccess / DuringSelection | PublicSelection / B | Source idol must be actor idol; summoned characterOwner is actor. |
| `idol.active.fetchTabiOrRestBoongAndFetchBoth` | IdolActive | `EffectManager.ResolveFetchTabiOrRestBoongAndFetchBothEffect`; online Tabi chained selection | `action.actor` idol | Enhanced: public own-broadcast #뿡댕이 cost; private deck #타비/#뿡댕이 choices | Basic one deck-to-hand or enhanced cost to rest plus two private draws | SelectionRequest, PrivateSelectionPayload, CardDraw, CardZoneMove, ActionState, Log | OnSuccess / FallbackBasic | PrivateSelection / C | Source idol actor-owned; #뿡댕이 cost local condition requires `slot.owner == actor` and `characterOwner == actor`. |
| `idol.active.fullHealOneControlled` | IdolActive | `EffectManager.ResolveIdolFullHealOneControlledEffect`; online selection flow | `action.actor` idol | Public controlled face-up character target | Heal selected character to max HP | SelectionRequest, FieldStat, Viewer, ActionState, Log | OnSuccess / DuringSelection | PublicSelection / B | Controlled target is `characterOwner == actor`, not own platform. |
| `idol.passive.allowActionOnAppearByTag` | Passive | `BattleManager` action/OnAppear handling | actor idol passive for matching characterOwner/tag | No user input | Allows action on appearance by tag | ActionState or included validation | No / None | IncludedInHostCalculation / E | Passive belongs to characterOwner idol; do not use local player's idol on non-host. |
| `idol.passive.collabNoKOByTag` | Passive | `EffectManager.HasIdolPassiveForSlot`, KO/collab resolution | participant `characterOwner` idol passive | No user input | Prevents KO during collab for tag | StartCollabResult/KO resolution, Log | No / None | IncludedInHostCalculation / E | Idol lookup by `slot.characterOwner`. |
| `idol.passive.collabTensionByCurrentHpForTag` | Passive | `EffectManager.HasIdolPassiveForSlot`, collab tension calculation | participant `characterOwner` idol passive | No user input | Tension bonus by current HP in Host collab calc | StartCollabResult stats, Log | No / None | IncludedInHostCalculation / E | Idol lookup by `slot.characterOwner`; no extra Delta. |

## Lists By Required Work

### SimplePublicDelta, Priority A

- `character.rest.gainViewers`
- `character.rest.loseViewers`
- `content.removeAllLastingContentsOnBoard`

### Auto Public Multi Target, Priority A

- `character.active.adjacentHpDownAndTensionUpForTag`

### Selection Flow, Priority B

- `character.active.forceBattleTargetAnywhere`
- `character.active.modifyTaggedOnBoard`
- `character.onAppear.callFromRestByTagToEmptyPlatforms`
- `content.forceOpponentFlipOrSack`
- `content.moveOwnCharToEmptyOrBattleIfTagged`
- `content.returnUpToNFromRestToDeck`
- `content.silenceCharacterCollabThisTurn`
- `idol.active.callFromRestByTagThenDonateViewers`
- `idol.active.fullHealOneControlled`

### Private Payload, Priority C

- `character.active.discardOneThenFetchContentByTagFromDeck`
- `character.active.peekTopAndTakeTaggedContents`
- `character.fetchCardsToHandByTags`
- `content.drawThenDiscard`
- `content.forceOpponentSummonOrSackFromHand`
- `content.peekTopAndTakeTaggedCharacterOrBottom`
- `content.redrawIfBehindAndUniverseOnly`
- `idol.active.fetchTabiOrRestBoongAndFetchBoth`

### Persistent Status, Priority D

- `broadcast.always.disableIdolActiveAndLockMoveOnEnter`
- `broadcast.always.noFaceDownSummonAndDisablePreCollabEffects`
- `character.active.adjacentOppCollabTensionDeltaThisTurn`
- `character.onAppear.adjacentOppCollabTensionDeltaThisTurn`
- `character.passive.doubleStepMoveNoJump`
- `character.rest.reduceOpponentCollabTensionOnCollab`
- `content.forbidOpponentAttackUntilNextTurn`
- `content.invertNegativeAmountForTagThisTurn`
- `content.lasting.buffTagTensionAndHp`
- `content.lockBroadcastIdNoMoveNoKOUntilNextEnd`

### Included In Host Calculation, Priority E

- `broadcast.always.prepViewersAndHealBonus`
- `broadcast.always.prepViewersAndOccupantHpDelta`
- `broadcast.always.taggedOccupantPrepViewersBonus`
- `character.passive.adjacentCollabTensionDeltaForTag`
- `character.passive.reduceOwnerPrepViewers`
- `character.passive.viewersBonusIfAdjacentToTag`
- `idol.passive.allowActionOnAppearByTag`
- `idol.passive.collabNoKOByTag`
- `idol.passive.collabTensionByCurrentHpForTag`

### Manual Trigger Candidate / Deferred, Priority F

- `broadcast.always.gainViewersWhenOccupantLeaves`
- `content.collabClicheSpendBuffRefund`
- `content.postCollabHealOwnParticipant`
- `content.postCollabTabiBoostAndRebattle`

## Owner / Remap Risk

High risk:

- `content.moveOwnCharToEmptyOrBattleIfTagged`: `targetSlot.owner` must never replace moving `characterOwner`.
- `idol.active.fullHealOneControlled`: target candidate is `characterOwner == actor`, not platform ownership.
- `idol.active.callFromRestByTagThenDonateViewers`: source idol is actor idol; called character owner remains actor.
- `idol.active.fetchTabiOrRestBoongAndFetchBoth`: source idol is actor idol; enhanced #뿡댕이 cost uses local condition `slot.owner == actor && characterOwner == actor`.
- `character.onAppear.callFromRestByTagToEmptyPlatforms`: destination slot may be opponent platform; characterOwner remains effect owner.
- `content.silenceCharacterCollabThisTurn`: status target slot id and characterOwner must remap together.
- `character.active.adjacentHpDownAndTensionUpForTag`: source must be `characterOwner == actor`; `slot.owner` participates in adjacency geometry only. Cross-owner adjacency must match local `EffectTargetingService.AdjacentToSource`; HP 0 rest owner is target `characterOwner`.
- `content.lockBroadcastIdNoMoveNoKOUntilNextEnd`: lock applies to broadcast/slot; occupant owner may differ.
- `broadcast.always.disableIdolActiveAndLockMoveOnEnter`: disable target is occupant owner.
- `broadcast.always.gainViewersWhenOccupantLeaves`: leaving owner must be captured before clearing source slot.
- `content.forceOpponentSummonOrSackFromHand`: private hand owner is opponent of actor, not local opponent after remap.
- idol passives (`idol.passive.*`): idol lookup must use participant/source `characterOwner`.

General remap checklist:

- Remap `result.actor`, `currentTurnPlayer`, all slot ids, all Delta owner fields, and selection request owner fields on non-host.
- Do not remap card instance ids.
- Private payload sanitization happens before or during per-recipient dispatch; sanitized opponent result must still keep public card counts and public zone movement.
- For field moves, serialize both `fromSlotId` / `toSlotId` and moving `owner` in `CardZoneMoveDelta`.
- For PublicAutoResolveMultiTarget, source validation uses `characterOwner == actor`; target ownership follows each effectRef's local filter. `slot.owner` may be required for board geometry such as adjacency, but must not replace `characterOwner` for card ownership or rest owner.
- Host slot coordinates and client UI perspective must be remapped consistently before applying adjacency-related visual updates.

## Recommended Implementation Order

1. Implement more PublicSelectionRequired effects first; they reuse the existing request/response validation path most directly.
2. Implement PublicSelectionAutoSingle effects next; they need both auto-resolve and selection request branches.
3. Treat PublicAutoResolveMultiTarget effects as Auto Resolve Multi Target work, not Selection Flow work. Host should emit direct public Deltas for all valid local candidates.
4. Implement PrivateSelection after public request/response and auto-resolve policies are stable.
5. Implement PersistentStatus after the StatusDelta registry and expiry policy are fixed.

## Result Apply Order

For Host-authored effect results, clients must apply stat changes before authoritative field-character rest moves:

1. Message / viewer / reveal deltas.
2. Field content metadata.
3. `FieldStatDelta`.
4. `CardZoneMoveDelta`, including `FieldCharacter -> RestZone` generated by Host after HP reaches 0.
5. Card draw, status, action state, and deck order deltas.

Clients must not independently recompute HP 0 rest movement from local stat application. The Host emits the `CardZoneMoveDelta` and merges supported OnRest simple deltas. Rest owner for a field character is always the target `characterOwner`.

## Recommended Next Infrastructure

1. `EffectLocalParityProbe`: a debug-only helper that runs the local candidate builders and online candidate builders for a given effectRef, logging owner, slot owner, characterOwner, and candidate match.
2. `PrivateSelectionPayload` shape: owner-only candidate ids, public candidate descriptors, sanitized opponent payload, request id chain id, and deck order authority fields.
3. Unified `StatusDelta` registry: turn expiry, phase expiry, slot/card target, stack policy, and Host-only validation hooks.
4. `ManualTriggerWindow` model for PreCollab/PostCollab/OnRest/OnLeave candidates so hand PostCollab content is offered, not auto-fired.
5. `HostCalculationAudit` logs for IncludedInHostCalculation effects to prove no duplicate Delta is emitted.

## Source Files Consulted

- `Assets/Resources/cards.json`
- `Assets/Scripts/EffectManager.cs`
- `Assets/Scripts/BattleManager.cs`
- `Assets/Scripts/BattleActionResult.cs`
- `Assets/Scripts/CardFunctionAuditManager.cs`
- `Assets/Scripts/MovementManager.cs`
- `Assets/Scripts/SummonManager.cs`
