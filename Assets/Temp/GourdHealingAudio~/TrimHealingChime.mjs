import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export class TrimHealingChime {
  async run() {
    const folder = path.dirname(fileURLToPath(import.meta.url));
    const decoded = await fs.readFile(path.join(folder, 'HealingChimeDecoded.f32'));
    const rate = decoded.readInt32LE(0), channels = decoded.readInt32LE(4);
    const sampleCount = decoded.readInt32LE(8);
    if (decoded.length !== 12 + sampleCount * 4) throw new Error('오디오 데이터 크기가 올바르지 않습니다.');
    const frames = sampleCount / channels;
    const samples = Float64Array.from({ length: frames }, (_, frame) => {
      let sum = 0;
      for (let channel = 0; channel < channels; channel++) sum += decoded.readFloatLE(12 + (frame * channels + channel) * 4);
      return sum / channels;
    });
    const window = Math.round(rate * 0.005), envelope = [];
    for (let first = 0; first < frames; first += window) {
      let energy = 0;
      for (let frame = first; frame < Math.min(frames, first + window); frame++) energy += samples[frame] ** 2;
      envelope.push(Math.sqrt(energy / window));
    }
    // 최대 구간보다 40dB 작은 영역을 무음으로 보고, 음의 시작을 자르지 않게 짧은 여유를 둔다.
    const threshold = Math.max(...envelope) * 0.01;
    const active = envelope.map((value, index) => value >= threshold ? index : -1).filter(index => index >= 0);
    if (!active.length) throw new Error('치유 사운드가 무음입니다.');
    const firstFrame = Math.max(0, active[0] * window - Math.round(rate * 0.005));
    const lastFrame = Math.min(frames, (active.at(-1) + 1) * window + Math.round(rate * 0.015));
    const output = samples.slice(firstFrame, lastFrame);
    const smooth = value => { const x = Math.max(0, Math.min(1, value)); return x * x * (3 - 2 * x); };
    for (let frame = 0; frame < output.length; frame++) {
      output[frame] *= smooth(frame / (rate * 0.005)) * smooth((output.length - 1 - frame) / (rate * 0.04));
    }
    const wave = Buffer.alloc(44 + output.length * 2);
    wave.write('RIFF', 0); wave.writeUInt32LE(wave.length - 8, 4);
    wave.write('WAVEfmt ', 8); wave.writeUInt32LE(16, 16);
    wave.writeUInt16LE(1, 20); wave.writeUInt16LE(1, 22);
    wave.writeUInt32LE(rate, 24); wave.writeUInt32LE(rate * 2, 28);
    wave.writeUInt16LE(2, 32); wave.writeUInt16LE(16, 34);
    wave.write('data', 36); wave.writeUInt32LE(output.length * 2, 40);
    for (let frame = 0; frame < output.length; frame++) {
      wave.writeInt16LE(Math.round(Math.max(-1, Math.min(1, output[frame])) * 32767), 44 + frame * 2);
    }
    await fs.writeFile(path.join(folder, 'Trimmed_HealingChime.wav'), wave);
    const report = { originalDuration: frames / rate, sourceStart: firstFrame / rate, sourceEnd: lastFrame / rate,
      trimmedDuration: output.length / rate, sampleRate: rate, channels: 1, pitchAndSpeedChanged: false,
      silenceThresholdRelativeDb: -40, runtimeContactDuration: 1.147,
      firstSample: wave.readInt16LE(44), lastSample: wave.readInt16LE(wave.length - 2) };
    await fs.writeFile(path.join(folder, 'HealingTrimReport.json'), JSON.stringify(report, null, 2) + '\n');
    return report;
  }
}

export const trimReport = await new TrimHealingChime().run();
