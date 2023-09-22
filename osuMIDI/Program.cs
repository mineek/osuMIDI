using System;
using Melanchall.DryWetMidi;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace osuMIDI
{
    public class osuBeatmapConfig
    {
        public string audio_filename = "";
        public string title = "";
        public string title_unicode = "";
        public string artist = "";
        public string artist_unicode = "";
        public float hp_drain_rate = 5;
        public float circle_size = 4;
        public float overall_difficulty = 8;
        public float approach_rate = 9;
        public float slider_multiplier = 1.8f;
        public int offset = 0;
        public int beat_length = 500;
    }

    public class osuPostprocessor
    {
        public string output_path;
        public string audio_path;
        public osuBeatmapConfig beatmap_config;

        public osuPostprocessor(string output_path, string audio_path, string title, string artist, int bpm)
        {
            this.output_path = output_path;
            this.audio_path = audio_path;
            this.beatmap_config = new osuBeatmapConfig();
            this.beatmap_config.title = title;
            this.beatmap_config.artist = artist;
            this.beatmap_config.title_unicode = title;
            this.beatmap_config.artist_unicode = artist;
            this.beatmap_config.beat_length = 60000 / bpm;
            this.beatmap_config.audio_filename = "audio.wav";
        }

        public void generate(int[][] notes)
        {
            string[] hit_object_strings = new string[notes.Length];
            int prev_timestamp = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                int[] note = notes[i];
                if (note == null) continue;
                int timestamp = note[0];
                if (prev_timestamp != 0 && timestamp <= prev_timestamp)
                {
                    continue;
                }
                int x = note[1];
                int y = note[2];
                hit_object_strings[i] = $"{x},{y},{timestamp},1,0,0:0:0:0:";
                prev_timestamp = timestamp;
            }
            string template = System.IO.File.ReadAllText("template.osu");
            string hit_objects = string.Join("\n", hit_object_strings);
            foreach (System.Reflection.FieldInfo field in this.beatmap_config.GetType().GetFields())
            {
                template = template.Replace($"${field.Name}", field.GetValue(this.beatmap_config).ToString());
            }
            if (template.Contains("$offset"))
            {
                template = template.Replace("$offset", this.beatmap_config.offset.ToString());
            }
            template = template.Replace("$hit_objects", hit_objects);
            string temp_dir = System.IO.Path.GetTempPath() + "osuMIDI-" + new Random().Next(0, 1000000);
            System.IO.Directory.CreateDirectory(temp_dir);
            Console.WriteLine($"Generating wav using FluidSynth...");
            System.Diagnostics.Process.Start("fluidsynth.exe", $"-F {temp_dir}\\audio.wav -T wav -r 44100 -g 1.0 -i \"soundfont.sf2\" \"{this.audio_path}\"").WaitForExit();
            Console.WriteLine($"Generated wav at {temp_dir}\\audio.wav");
            System.IO.File.WriteAllText(System.IO.Path.Combine(temp_dir, "template.osu"), template);
            if (System.IO.File.Exists(this.output_path + ".osz"))
            {
                System.IO.File.Delete(this.output_path + ".osz");
            }
            System.IO.Compression.ZipFile.CreateFromDirectory(temp_dir, this.output_path + ".osz");
            Console.WriteLine($"Beatmap generated at {this.output_path}.osz");
        }
    }

    public class osuMIDI
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to osu!MIDI!");
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: osuMIDI.exe <midi_path> <output_path>");
                return;
            }
            string midiPath = args[0];
            string title = args[2];
            string artist = "Generated using mineekware";
            string output_path = args[1];
            int bpm = 0;
            var midiFile = MidiFile.Read(midiPath);
            TempoMap tempoMap = midiFile.GetTempoMap();
            bpm = (int)tempoMap.GetTempoAtTime(new MetricTimeSpan(0)).BeatsPerMinute;
            Console.WriteLine($"BPM: {bpm}");
            osuPostprocessor postprocessor = new osuPostprocessor(output_path, midiPath, title, artist, bpm);
            int[][] notes = new int[midiFile.GetTimedEvents().Count][];
            Console.WriteLine($"Found {midiFile.GetTimedEvents().Count} notes");
            int i = 0;
            int baseX = 192;
            int baseY = 192;
            foreach (var timedEvent in midiFile.GetTimedEvents().Where(e => e.Event.EventType == MidiEventType.NoteOn))
            {
                MetricTimeSpan time = timedEvent.TimeAs<MetricTimeSpan>(tempoMap);
                NoteOnEvent noteOnEvent = (NoteOnEvent)timedEvent.Event;
                if (noteOnEvent.Channel != 0)
                {
                    continue;
                }
                int timestamp = (int)time.TotalMilliseconds;
                // pls someone improve this "algorithm" for finding coordinates
                int x = baseX + new Random().Next(-192, 192);
                int y = baseY + new Random().Next(-192, 192);
                notes[i] = new int[] { timestamp, x, y };
                i++;
                Console.WriteLine($"Note {i} at {timestamp}ms");
            }
            postprocessor.generate(notes);
        }
    }
}