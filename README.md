# Harmony in Silence
A Speech-to-Text Empowerment Initiative for the Hard of Hearing Community.

## Inspiration
As Discord grows as a platform, each user faces their own unique challenges. A large problem that many people face is difficulty using voice channels. Some people with audio processing disorders may be able to hear, but have difficulty understanding spoken language. Others may be hard of hearing or even completely deaf, and unable to hear at all and thus, making voice channels feel like a complete social barrier.

We wanted to create a solution that would allow these people to participate in voice channels, and feel included in the community. Why? Because they're worth it.

## What it does
Harmony in Silence is a Discord bot that transcribes voice channels in real-time. It uses the Deepgram API to transcribe the audio, and then sends the text to the channel. This allows people who are hard of hearing to participate in voice channels, and feel included in the community. Sometime in the future, we hope to allow users to read the subtitles through either a web interface or - even better - through a Discord bot stream.

## Roadmap
| Feature       | Description                                                                                 | Status          |
|---------------|---------------------------------------------------------------------------------------------|-----------------|
| Hello World   | Get the bot up and running                                                                  | Completed Day 1 |
| Voice Gateway | Setup a `/join` command to join voice channels                                              | In Progress     |
| Transcription | Record the audio from voice channels and send them in batches to the voice channel messages | Not Started     |
| Web Interface | Create an accessible web interface to view the transcriptions in real-time                  | Not Started     |

Nearly all of the major dependencies were created by me:
- [DSharpPlus.Commands](https://github.com/DSharpPlus/DSharpPlus/pull/1680/)
- [DSharpPlus.VoiceLink](https://github.com/OoLunar/DSharpPlus.VoiceLink/)
- [DeepgramSharp](https://github.com/OoLunar/DeepgramSharp)

These dependencies are all open-source and available on GitHub.