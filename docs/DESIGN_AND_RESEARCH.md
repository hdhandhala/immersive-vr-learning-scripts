# Design & Research Notes

How the two VR learning systems in this repository were designed, why each interaction works the way it does, how the work was evaluated, and how it could be evaluated further.

**Contents:** [Design goals](#design-goals) · [Process](#process) · [Interaction design rationale](#interaction-design-rationale) · [Guidance & UI](#guidance--ui) · [Comfort & performance](#comfort--performance) · [User study](#user-study) · [Evaluation plan](#evaluation-plan) · [Limitations](#limitations)

---

## Design goals

1. **Learning by doing.** The learner's own movement should carry out the task, so the body learns the procedure, not just the eyes.
2. **Feedback you can feel.** Every meaningful action should answer through more than one sense: touch (haptics), sound and visuals.
3. **Mistakes that teach.** Errors should be prevented by physical constraints where possible and explained where not.
4. **Guidance without control.** Help should be available when needed and step back when it isn't, so learners keep autonomy over pace and order.
5. **Self-directed use.** The system should work without a trainer standing next to the learner.

---

## Process

The two projects form one design cycle:

```
Project thesis                         Master's thesis
build VR environment ─► user study ─► findings ─► redesign: physical interaction,
                       (VR vs. document)          on-demand guidance, learner control
```

The project thesis showed that VR learners understood spatial relationships better but wanted more control over the pace of narrated content. The Master's thesis responded with interactions driven by the learner's own movement, instructions that appear per station, and play/pause and step controls that stay with the learner.

---

## Interaction design rationale

### Drilling

| | |
|---|---|
| **Problem** | An early, time-based version (hold the trigger, wait, hole appears) completed the task without the learner experiencing it. |
| **Decision** | Hole depth progresses in stages only as far as the drill bit physically penetrates the surface; pulling back stops progress. Only assigned surfaces can be drilled. |
| **Principles** | Embodied interaction, natural mapping, constraints. |
| **Feedback** | Haptic intensity increases with depth; separate audio for drilling in air and in wood; dust bursts at each new stage. |
| **Sequencing** | A screw socket only appears once the hole is complete, so the correct order of work is enforced by the world itself. |
| **Code** | `Interactions/Drilling/SuperDrilling.cs`, `Shaders/Interaction/DrillHole.shader` |

### Screwing

| | |
|---|---|
| **Problem** | Real screwing is twist, release, re-grip, twist again. A naive mapping unscrews slightly on every re-grip. |
| **Decision** | Only rotation around the hole axis is used and converted to depth through the thread pitch (turns × pitch = depth). A ratchet-style direction lock ignores small backward twists and reverses only on a deliberate opposite turn. |
| **Principles** | Natural mapping, error tolerance, constraints (the screw only seats when its tip is at the entry and its shaft is aligned). |
| **Feedback** | Haptic clicks scale with twist speed. |
| **Code** | `Interactions/Screwing/Screw.cs`, `ScrewSocket.cs`, `ScrewdriverTwistDriver.cs` |

### Wire winding and cutting

| | |
|---|---|
| **Problem** | Unity has no rope; rigid chains look stiff and tangle. Winding must respond to the hand, not to a button. |
| **Decision** | A custom Verlet rope rendered as a tube. Near the post it switches to a helix capture: the hand orbiting the post adds turns, mapped to arc length so wire is consumed realistically. Direction is enforced; start and release thresholds filter accidental movements. |
| **Principles** | Embodied interaction, error tolerance, physical plausibility. |
| **Feedback** | The reel has inertia and friction, spins as wire is pulled off and vibrates with its speed; the cut tail falls away as its own object. |
| **Code** | `Interactions/WireWinding/VRCoilWithRopeMesh.cs`, `WireReelFeederRealistic.cs`, `OneShotWireCutter.cs` |

### Gluing and placement

| | |
|---|---|
| **Problem** | An earlier prototype dropped separate glue blobs that read as dots, not as glue. Placement errors were not distinguishable. |
| **Decision** | One continuous procedural bead: thicker where the hand slows, lagging like a viscous liquid, with rounded ends. It dries over 20 s (glossy → matte) before the cell locks. |
| **Principles** | Visibility of system state, time as a meaningful constraint, explaining errors. |
| **Feedback** | The placement grid shows *missing glue*, *wrong orientation* and *ready* as separate colour states, with a soft haptic confirmation on correct placement. |
| **Code** | `Interactions/Gluing/GlueGunMeshExtruder_Final.cs`, `PlacementGridManager.cs`, `BatteryGlueState.cs`, `Shaders/Interaction/HotGlueDrying_URP.shader` |

---

## Guidance & UI

| Element | Design intent | Code |
|---|---|---|
| **Holographic instructor** | A presenter video keyed onto a quad above a holopad beside the learner, so guidance shares the learner's space instead of floating as a 2D overlay. Shown per station, on demand. | `Shaders/Hologram/*`, `Guidance/UI/SimpleVRUIManager.cs` |
| **Zone-triggered content** | Entering a station loads only that station's video, steps and panel, which keeps information local and reduces search. | `Guidance/UI/SimpleZoneTrigger.cs` |
| **Learner-controlled playback** | Play/pause, forward/back and *Mark as Done* let learners set their own pace. | `SimpleVRUIManager.cs` |
| **Floor path** | A generated ribbon leads to the next station; finished sections fade, showing progress spatially. | `Guidance/Navigation/FloorPathRibbon.cs` |
| **Pressable 3D buttons** | Physical press depth plus shader-driven hover and pressed states make buttons look and feel pushable. | `Guidance/UI/VRPressableButton.cs`, `Shaders/Guidance/VRButtonMaster.shader` |
| **Session flow** | Onboarding places the learner exactly at the start point; an end panel offers to continue exploring or exit; objects can be reset, so mistakes are recoverable. | `Guidance/SessionFlow/*` |

---

## Comfort & performance

- **Frame rate as a comfort requirement.** The standalone environment (project thesis) uses baked lighting, baked reflection probes, occlusion culling, static batching, 2K texture limits and Vulkan to keep frame rate steady and avoid discomfort.
- **Locomotion choice.** Teleportation and continuous movement are both available, with fixed teleport points and floor guides.
- **Seated or standing.** Interactions are reachable in both postures.
- **Offline by design.** No network dependency, so the system runs in any training room.

---

## User study

**Project thesis, 2025.** Pilot comparison of VR and document-based learning.

| | |
|---|---|
| **Design** | Between-subjects, two groups |
| **Participants** | 10 (engineering students and vocational trainees), 5 per group |
| **Conditions** | VR learning environment vs. PDF with the same content, about 20 minutes each |
| **Measures** | Identical 15-item knowledge test; post-test questionnaire with qualitative feedback |
| **Results** | The VR group scored higher on average, especially on spatial and component questions; the document group did slightly better on purely text-based facts. VR learners reported higher engagement and asked for more control over narration pace. |
| **Interpretation** | An indication, not a statistical result: the sample is small and analysis was descriptive. |

---

## Evaluation plan

How the Master's thesis system could be evaluated rigorously. **This is a plan, not completed work.**

| Question | Design | Measures |
|---|---|---|
| Does movement-driven interaction improve procedural learning? | Within- or between-subjects: physics-based vs. snap-based version of the same tasks | Task time and errors (loggable from the existing validation states), knowledge test, delayed retention test, transfer to a real task |
| What does the interaction cost the learner? | Same conditions | NASA-TLX (workload), SUS (usability), simulator sickness questionnaire |
| How present do learners feel? | Same conditions | IPQ presence questionnaire |
| How does the virtual instructor's presentation affect trust and compliance? | Vary placement (beside vs. screen), fidelity (video vs. avatar), and timing (on demand vs. always on) | Trust scales, instruction-following rate, qualitative interviews |
| Which haptic patterns help? | Vary haptic intensity and patterns using the existing parameters | Error rates, subjective ratings |

Analysis would use mixed-effects models or repeated-measures ANOVA for quantitative data and thematic analysis for interviews.

---

## Limitations

- The project thesis study was small (n = 10) and descriptive.
- The Master's thesis system has not yet been evaluated in a controlled study.
- Tested on Meta Quest 3 only; OpenXR should allow other headsets but this hasn't been verified.
- Content comes from one domain (electric drivetrains); generalisation to other tasks is assumed, not shown.
