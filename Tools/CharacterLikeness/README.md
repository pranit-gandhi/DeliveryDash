# Free likeness reconstruction

1. Use Python 3.11 with `mediapipe==0.10.32`, Pillow, numpy, opencv-python.
2. Download Google's face_landmarker.task to /tmp/face_landmarker.task (see reconstruct.py).
3. Run `python reconstruct.py references/Characters/PERSON.jpg references/Characters/PERSON-face.json` from the project root, adjusting script path to Tools/CharacterLikeness.
4. Run Blender 5.2: `Blender --background --python Tools/CharacterLikeness/build_character.py -- PERSON`.
5. In the TownDelivery scene, choose **DeliveryDash > Install free customer likenesses** outside Play mode. Then Play.

Available PERSON names: sam-altman, dario-amodei, donald-trump, elon-musk, jensen-huang.

Outputs: editable Blender files under references/Characters; FBX, textures, prefabs and materials under Assets/DeliveryDash/Art/Characters/Likeness; front, three-quarter and profile renders under Captures.

The mesh is fitted to MediaPipe's landmark depth estimates from one image. This is not calibrated multiview reconstruction. Hair, ears, skull and body proportions are approximate; photo illumination and expressions are baked into the face. Close-up cinematic realism is not achieved. The runtime driver supplies skeletal walking and carry poses, without facial animation. Keep the photo licenses and MediaPipe Apache license with redistributed assets.
