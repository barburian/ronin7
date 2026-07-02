# Space Samurai — Phase 0 Setup (Human / Editor steps)

All gameplay code, asmdefs, input actions, and a one-click rig builder are already in the
project. These are the steps that **must** be done in the Unity Editor GUI / on a headset —
they can't be scripted from outside Unity. Do them in order.

## 1. Let packages resolve
Open the project in Unity 6 (6000.3.2f1). The Package Manager will fetch the XR packages
added to `Packages/manifest.json`:
`com.unity.xr.interaction.toolkit`, `com.unity.xr.openxr`, `com.unity.xr.management`,
`com.unity.xr.core-utils`, `com.unity.xr.hands`, `com.unity.xr.meta-openxr`.

> If any version fails to resolve, open **Window → Package Manager**, find the package, and
> click **Update** to the version Unity recommends for 6000.3. Versions in the manifest are
> best-effort; the exact patch may differ.

## 2. Enable XR Plug-in Management
**Edit → Project Settings → XR Plug-in Management** → **Install** if prompted.
- **PC tab (Windows/standalone):** check **OpenXR**.
- **Android tab:** check **OpenXR** (this is the Quest target).
- Under **OpenXR** (each tab), add interaction profiles:
  - **Oculus Touch Controller Profile** (Quest)
  - **Meta Quest Touch Pro / Plus** profiles if you have those controllers
  - Optionally a generic profile for other PCVR controllers.
- Android tab → OpenXR → enable the **Meta Quest** feature group.
- Resolve any yellow validation warnings (the **Fix All** button handles most).

## 3. Android / Quest player settings (for standalone builds)
**File → Build Profiles** (or Build Settings) → switch platform to **Android**.
**Project Settings → Player → Android:**
- Scripting Backend: **IL2CPP**, Target Architectures: **ARM64** only.
- Minimum API Level: **Android 12 (API 32)** or as required by current Meta SDK.
- Graphics APIs: **Vulkan** (remove OpenGLES if present).
- Color Space: **Linear**.
- Active Input Handling: **Input System Package (New)** — should already be set.

## 4. Quest-tuned URP renderer + quality (the perf budget)
PCVR uses the existing `Assets/Settings/PC_RPAsset.asset`. Add a mobile tier for Quest:
1. **Assets → Create → Rendering → URP Asset (with Universal Renderer)**; name it
   `Quest_RPAsset` and put it in `Assets/Ronin7/Settings/`.
2. On that URP asset set Quest-friendly values:
   - **HDR: off**, **MSAA: 4x** (or 2x if GPU-bound), **Render Scale: 1.0**.
   - **Main Light → Shadows:** Soft off / Hard, **Shadow Resolution low**, **Shadow Distance ~20m**.
   - **Additional Lights: Per Pixel**, low count, **Additional Light Shadows: off**.
   - **Depth/Opaque Texture: off** unless a shader needs them.
3. **Project Settings → Quality:** create/keep a low tier for Android pointing at
   `Quest_RPAsset`; set it as the default for the Android platform column.
4. **Project Settings → Graphics:** confirm the URP asset is assigned (PC tier → PC asset).

## 5. Build the rig + smoke scene (one click each)
Top menu (added by our Editor scripts):
- **Tools → Space Samurai → Build XR Rig Prefab** → creates
  `Assets/Ronin7/Prefabs/XR Rig.prefab`.
- **Tools → Space Samurai → Build Phase 0 Smoke Scene** → creates and opens
  `Assets/Ronin7/Scenes/Phase0_VRSmoke.unity` (floor, reference cube, light, rig).

## 6. Test (exit criteria for Phase 0)
- **PCVR:** connect Quest via Link / use any OpenXR PCVR headset, press **Play**.
- **Standalone:** add the smoke scene to Build Profiles → **Build And Run** to the Quest.
- Confirm: head tracking, you see two cube "hands" tracking your controllers, **left stick
  moves** (head-relative), **right stick snap-turns**, gravity keeps you on the floor.

Once this works on the device, Phase 0 is done and we move to Phase 1 (grab interaction +
player health) and Phase 2 (the sword combat milestone).

## Notes on what was intentionally NOT scripted
- Locomotion uses our own `ContinuousLocomotion` (Input-System-driven) instead of XRI's
  locomotion providers, to stay robust across XRI versions. Teleport is added in a later phase.
- Hands are placeholder cubes; XRI grab interactors and real hand visuals come in Phase 1.
