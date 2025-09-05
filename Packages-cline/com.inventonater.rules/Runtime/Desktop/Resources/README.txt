BEEP SOUND REQUIREMENT
======================

Place a short beep sound file named "beep.wav" in this Resources folder
for the audio.play service to work properly.

The audio file should be:
- Format: WAV (recommended) or any Unity-supported format
- Duration: Short (0.1 - 0.5 seconds)
- Name: beep.wav (exactly)

You can:
1. Create your own beep sound
2. Use a free sound from freesound.org
3. Generate one with an audio tool
4. Use Unity's built-in AudioSource testing sounds

Without this file, the rules engine will still work but audio
actions will log a warning and not play any sound.
