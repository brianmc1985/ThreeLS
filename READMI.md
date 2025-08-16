# `ThreeLSControl` - Advanced Lipsync and Facial Animation

`ThreeLSControl` is a Unity script designed for creating realistic, audio-reactive character animations. It combines automated lipsync with procedural head motion, eye blinking, and eyebrow movements, allowing you to generate expressive animations from an audio file.

## Features

* **Automated Lipsync:** Analyzes audio input in real-time to drive blend shapes for lipsync.
* **Dynamic Head Motion:** Procedurally generates subtle head movements (yaw, pitch, roll) that react to speech energy.
* **Syllable Impulses:** Detects vocal transients to create rhythmic head "impulses" for more natural animation.
* **Procedural Eye & Eyebrow Animation:** Includes automatic blinking and eyebrow pulsing with configurable asymmetry and timing jitter for added realism.
* **Animation Export:** Records all generated lipsync, eye, eyebrow, and head movements into a standard `.anim` clip for use with Unity's Animator system.
* **Configurable Parameters:** Fine-tune the behavior of all systems through a user-friendly Inspector interface.

---

## How to Use

### 1. Setup

1.  **Attach the Script:** Add the `ThreeLSControl.cs` script to the root `GameObject` of your character. This `GameObject` must also have an `AudioSource` component.
2.  **Add Your Audio Clip:** Drag and drop an audio clip into the `AudioSource` component's **Audio Clip** field.
3.  **Find Blend Shapes:** With the `ThreeLSControl` component selected in the Inspector, click the **Load Available Blend Shapes** button. This will populate a list of all blend shapes found on `SkinnedMeshRenderer` components in your character's hierarchy.

### 2. Configure Blend Shapes

In the `ThreeLSControl` Inspector, expand the **Blend Shape Names** section.

* **Lipsync:** Drag and drop or manually select the blend shape names from the dropdowns for the `Kiss`, `Lips Closed`, and `Mouth Open` groups. You can add multiple shapes to each list.
* **Eyes & Eyebrows:** Assign the blend shapes for the `Eye Close` and `Eyebrow` movements for both the left and right sides.

### 3. Configure Head Motion

Expand the **Head Motion** section.

1.  **Assign Transforms:**
    * **Head Bone/Control:** Drag and drop the `Transform` (usually a bone) that controls your character's head rotation.
    * **Head Animation Root:** Drag and drop the root `Transform` of your character's skeleton (e.g., the `GameObject` with the `Animator` component). This is crucial for the animation export to work correctly.

2.  **Adjust Parameters:**
    * **`Reactive Amount`:** Controls how much the head moves in response to the voice's energy.
    * **`Syllable Impulse`:** Determines the strength of the head "nod" for each syllable.
    * **`Noise Amp Deg`:** Sets the amount of subtle, randomized head jitter.

### 4. Animation Export

The script can record and export all the generated animations into a `.anim` file.

1.  **Start Recording:** In the Inspector, click the **Start Recording** button. A file save dialog will appear.
2.  **Save File:** Choose a name and location for your `.anim` file inside your project's `Assets` folder.
3.  **Automatic Play:** The Unity Editor will automatically enter **Play Mode** and begin the recording. The animation starts after the initial `startDelaySeconds` period.
4.  **Stop Recording:** The recording will automatically stop when the audio clip finishes playing. Alternatively, you can click the **Stop Recording** button at any time.

Upon stopping, the `.anim` file will be created at the specified path. You can then drag this clip into an Animator Controller to play the generated animation.

---

## Technical Details

The lipsync logic is based on a frequency-domain analysis of the audio signal using an FFT (Fast Fourier Transform). It divides the frequency spectrum into three main bands to calculate weights for the Kiss, Lips Closed, and Mouth Open blend shapes.

* **Kiss:** Driven by a combination of low and mid-frequencies.
* **Lips Closed:** Primarily controlled by high-frequency content (e.g., sibilants and fricatives).
* **Mouth Open:** A contrast-based value, where the presence of mid-frequencies (vowels) opens the mouth, while high-frequencies close it.

The procedural head and eye movements are driven by `Coroutines` and real-time calculations in `Update()` and `LateUpdate()`. `LateUpdate()` is used for head motion to ensure the procedural rotation overrides any existing animation on the same `Transform`.