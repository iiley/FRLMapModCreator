By creating, submitting, or uploading Content for FR Legends, you acknowledge that you have read, understood, and agreed to the terms of this Agreement:  
[FR Legends Custom Track Creator Agreement](FR_Legends_Custom_Track_Creator_Agreement.md)

---

## Tutorials

### Installation

Download and the open this project in Unity, only **Unity 2022.3.x** is supported.
Make sure your unity have both **Android Build Support** and **iOS Build Support** modules installed.
![Step 0 - Unity Installation](/docs/images/unity_modules.jpg)

### Create a Custom Track


Create your custom track by modifying the provided `MapExample.unity`.  
Save your scene as, for example, `YourTrack.unity`.


> Please note that your final built asset bundle is limited to 10MB, if it is larger than that, you need to optimize the Mesh and Textures.
> Suggestion for optimization:
> - Use Mesh Simplification tools to reduce polygon count, less than 10,000 triangles is suggested.
> - Compress textures using appropriate formats (e.g., ASTC/ETC for Android, ASTC/PVRTC for iOS).
> - Remove any unused assets from the scene.

> About the Layers, make sure the ground is using the "Ground" layer, Walls is using "Wall" layer, and all other objects are using the "Default" layer.

---

### UGC Map Manager

Open the UGC Map Manager from the Unity menu:

**Tools → UGC Map Manager**

![Step 1 - Login Account](/docs/images/login.jpg)

After logging in, the UGC Map Manager window will appear.

> If you have not created any maps yet, the list will be empty.

![Step 2 - UGC Map Manager](/docs/images/manager.jpg)

If the currently opened scene is not linked to an existing map, click **Create New Map** to create one.

![Step 3 - Create Track](/docs/images/create_track.jpg)

After creating a new map, the map editing window will open automatically.

![Step 4 - Edit Track](/docs/images/edit.jpg)

- After editing text fields, click **Save** to apply the changes.
- Image changes are saved automatically when a new image is selected.
- Click **Build & Upload** to build the scene and upload the map files to the server.

If the map has already been published, you must click **Create Version** first to generate a new version before making changes or uploading updated files.

---

Once the build and upload process is complete, return to the manager window and click **Publish** to submit the track.

![Step 5 - Publish Track](/docs/images/publish.jpg)

After publishing, the track will enter the review process and await approval by the FR Legends team.
> You may delete a map at any time if it has not been published or is not currently under review. For published maps, you must contact the FR Legends team to request removal.


---

### Test Map Draft

> **Important:**  
> You must test the uploaded map before publishing it.

1. Open **FR Legends** on your mobile device.
2. Navigate to **Custom Tracks → My Drafts**.
3. Select your uploaded track to open its details page.
4. Tap **Play** to test the track in-game and verify that it runs correctly.

![Step 6 - Test Track](/docs/images/drafts.jpg)

If you encounter any issues:
- Fix them in the Unity scene
- Click **Build & Upload** again to update the draft

Once the track passes review and is approved by the FR Legends team, it will become available in the game for all players to download and play.

> You may need to click **Refresh List** in the UGC Map Manager to see the updated review status.

---
