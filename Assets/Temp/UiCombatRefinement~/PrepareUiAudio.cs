using System;
using System.IO;
using UnityEditor;

public static class PrepareUiAudio
{
    public static object Run()
    {
        string folder = "Assets/Arts/Audio/MountainPass";
        Directory.CreateDirectory(folder);
        string[] names = {"FanWhoosh", "WoodStrike", "WindHit", "PlayerHurt", "GuardianSlam", "ShrineBell", "MountainWind", "Footstep", "Dodge", "GourdDrink", "MenuConfirm", "SpiritAwaken", "Victory", "Defeat", "FanEquip"};
        double[] durations = {.42, .28, .34, .4, 1.15, 2.8, 8, .19, .5, .85, .24, 1.5, 2.4, 1.6, .28};
        const int rate = 44100;
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index]; double duration = durations[index];
            var random = new Random(825 + index);
            double[] samples = new double[(int)(rate * duration)];
            double low = 0, peak = 0;
            for (int frame = 0; frame < samples.Length; frame++)
            {
                double t = frame / (double)rate, p = t / duration;
                double noise = random.NextDouble() * 2 - 1;
                low += .075 * (noise - low);
                double high = noise - low, value = 0;
                if (name == "MountainWind")
                {
                    // 정수 주기 성분을 합쳐 환경음 루프의 이음새에서 클릭이 생기지 않게 한다.
                    for (int k = 43; k < 281; k += 13) value += Math.Sin(2 * Math.PI * k * t / duration + k * 1.71) / Math.Pow(k, .63);
                    value *= .8 + .2 * Math.Cos(2 * Math.PI * p);
                }
                else if (name == "FanWhoosh" || name == "Dodge" || name == "WindHit")
                {
                    value = (low * 1.8 + high * .07) * Math.Pow(Math.Sin(Math.PI * p), name == "WindHit" ? 1 : 2);
                    if (name == "WindHit") value += Math.Sin(2 * Math.PI * (380 * t - 220 * t * t)) * Math.Exp(-t * 18) * .18;
                }
                else if (name == "WoodStrike" || name == "Footstep" || name == "FanEquip")
                {
                    double tone = name == "WoodStrike" ? 165 : name == "Footstep" ? 90 : 420;
                    value = (Math.Sin(2 * Math.PI * tone * t) * .3 + Math.Sin(2 * Math.PI * tone * 2.73 * t) * .14) * Math.Exp(-t * 26);
                    value += (low * .85 + high * .13) * Math.Exp(-t * (name == "FanEquip" ? 38 : 20));
                }
                else if (name == "GuardianSlam" || name == "PlayerHurt")
                    value = (.6 * Math.Sin(2 * Math.PI * (65 * t - 18 * t * t)) + low * 1.4) * Math.Exp(-t * 6) + high * .18 * Math.Exp(-t * 38);
                else if (name == "GourdDrink")
                {
                    foreach (double start in new[] {.06, .24, .43, .64})
                    {
                        double dt = t - start;
                        if (dt > 0 && dt < .16) value += (.26 * Math.Sin(2 * Math.PI * (290 * dt + 1900 * dt * dt)) + low * .38) * Math.Pow(Math.Sin(Math.PI * dt / .16), 2);
                    }
                }
                else if (name == "ShrineBell" || name == "MenuConfirm" || name == "Victory" || name == "Defeat")
                {
                    double[] notes = name == "ShrineBell" ? new[] {660d} : name == "MenuConfirm" ? new[] {880d, 1320d} : name == "Victory" ? new[] {392d, 523.25, 659.25, 783.99} : new[] {293.66, 220, 146.83};
                    for (int j = 0; j < notes.Length; j++)
                    {
                        double dt = t - j * (name == "MenuConfirm" ? .018 : .22), frequency = notes[j];
                        if (dt < 0) continue;
                        value += (Math.Sin(2 * Math.PI * frequency * dt) * .34 * Math.Exp(-dt * 2.4) +
                            Math.Sin(2 * Math.PI * frequency * 2.76 * dt) * .12 * Math.Exp(-dt * 4.2) +
                            Math.Sin(2 * Math.PI * frequency * 5.4 * dt) * .035 * Math.Exp(-dt * 7)) * Math.Min(1, dt * 180);
                    }
                }
                else value = (low * 1.4 + Math.Sin(2 * Math.PI * (110 * t + 55 * t * t)) * .13) * Math.Pow(Math.Sin(Math.PI * p), 2);
                value *= name == "MountainWind" ? 1 : Math.Min(1, t / .008) * Math.Min(1, (duration - t) / .04);
                samples[frame] = value; peak = Math.Max(peak, Math.Abs(value));
            }
            double gain = (name == "MountainWind" ? .36 : .78) / Math.Max(.001, peak);
            using (var writer = new BinaryWriter(File.Create(folder + "/" + name + ".wav")))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (double sample in samples) writer.Write((short)Math.Round(Math.Max(-1, Math.Min(1, sample * gain)) * 32767));
            }
        }
        AssetDatabase.Refresh();
        return new {clips = names.Length, sampleRate = rate, folder};
    }
}
