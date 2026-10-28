import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// 게임 에셋에는 접근하지 않고, 이 폴더 안에 미리듣기 파일만 생성한다.
export class GourdHealingPreview {
  constructor(sampleRate = 44100) {
    this.sampleRate = sampleRate;
    this.seed = 20261028;
  }

  random() {
    this.seed = (Math.imul(this.seed, 1664525) + 1013904223) >>> 0;
    return this.seed / 4294967296;
  }

  smoothStep(value) {
    const progress = Math.max(0, Math.min(1, value));
    return progress * progress * (3 - 2 * progress);
  }

  addTone(output, { start = 0, duration, frequency, gain, attack = 0.025,
    decay = 3, harmonics = [[1, 1], [2, 0.18], [3, 0.035]], rise = 0,
    vibrato = 0.001, phase = 0 }) {
    const startFrame = Math.round(start * this.sampleRate);
    const frameCount = Math.min(Math.round(duration * this.sampleRate), output.length - startFrame);
    let currentPhase = phase;
    for (let frame = 0; frame < frameCount; frame++) {
      const time = frame / this.sampleRate;
      const progress = time / duration;
      const envelope = this.smoothStep(time / attack) * Math.exp(-decay * progress)
        * (1 - this.smoothStep((progress - 0.68) / 0.32));
      // 음높이를 미세하게 흔들어 기계적인 느낌을 줄이고 잔향을 부드럽게 한다.
      const pitch = frequency * (1 + rise * (1 - Math.exp(-time * 5)))
        * (1 + vibrato * Math.sin(time * Math.PI * 2 * 3.7));
      currentPhase += Math.PI * 2 * pitch / this.sampleRate;
      let sample = 0;
      for (const [ratio, amplitude] of harmonics) {
        sample += Math.sin(currentPhase * ratio) * amplitude;
      }
      output[startFrame + frame] += sample * envelope * gain;
    }
  }

  filter(input, type, frequency, quality = 0.707) {
    const omega = Math.PI * 2 * frequency / this.sampleRate;
    const cosine = Math.cos(omega);
    const alpha = Math.sin(omega) / (2 * quality);
    const denominator = 1 + alpha;
    let b0, b1, b2;
    if (type === 'lowpass') {
      b0 = (1 - cosine) / 2;
      b1 = 1 - cosine;
      b2 = b0;
    } else if (type === 'highpass') {
      b0 = (1 + cosine) / 2;
      b1 = -(1 + cosine);
      b2 = b0;
    } else {
      b0 = alpha;
      b1 = 0;
      b2 = -alpha;
    }
    b0 /= denominator; b1 /= denominator; b2 /= denominator;
    const a1 = -2 * cosine / denominator;
    const a2 = (1 - alpha) / denominator;
    const output = new Float64Array(input.length);
    let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
    for (let frame = 0; frame < input.length; frame++) {
      const sample = b0 * input[frame] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
      output[frame] = sample;
      x2 = x1; x1 = input[frame]; y2 = y1; y1 = sample;
    }
    return output;
  }

  addAir(output, { duration, gain, center, swell = 0.15 }) {
    const noise = Float64Array.from(output, () => this.random() * 2 - 1);
    const breath = this.filter(this.filter(noise, 'bandpass', center, 0.65), 'lowpass', 4400);
    for (let frame = 0; frame < output.length; frame++) {
      const time = frame / this.sampleRate;
      const progress = time / duration;
      if (progress >= 1) break;
      const envelope = this.smoothStep(time / swell) * Math.pow(1 - progress, 1.7)
        * (0.84 + 0.16 * Math.sin(Math.PI * 2 * 2.3 * time));
      output[frame] += breath[frame] * envelope * gain;
    }
  }

  reverb(input) {
    const output = Float64Array.from(input);
    const softened = this.filter(input, 'lowpass', 3300);
    // 짧게 흩어지는 반사음만 더해 전투 중에도 긴 잔향이 쌓이지 않게 한다.
    for (const [seconds, gain] of [[0.039, 0.12], [0.067, 0.10], [0.109, 0.08],
      [0.157, 0.065], [0.223, 0.048], [0.293, 0.033], [0.367, 0.021]]) {
      const delay = Math.round(seconds * this.sampleRate);
      for (let frame = delay; frame < output.length; frame++) {
        output[frame] += softened[frame - delay] * gain;
      }
    }
    return output;
  }

  renderWarmHealing() {
    const output = new Float64Array(Math.round(2.1 * this.sampleRate));
    this.addAir(output, { duration: 1.4, gain: 0.095, center: 1450, swell: 0.10 });
    // 낮은 온기 위에 배음을 쌓아, 체력이 차오르는 하나의 덩어리로 들리게 한다.
    for (const [frequency, gain, start, duration] of [
      [293.665, 0.16, 0, 1.55], [440, 0.10, 0.055, 1.60],
      [587.33, 0.085, 0.10, 1.68], [880, 0.030, 0.21, 1.45]]) {
      this.addTone(output, { frequency, gain, start, duration, attack: 0.16,
        decay: 1.8, rise: 0.012, harmonics: [[1, 1], [2, 0.16], [3, 0.025]] });
    }
    this.addTone(output, { frequency: 146.8325, duration: 1.1, gain: 0.05,
      attack: 0.07, decay: 3.0, harmonics: [[1, 1], [2, 0.12]] });
    // 작은 입자음은 낮게 섞어 알림음 같은 멜로디를 만들지 않는다.
    for (const [start, frequency, gain] of [[0.02, 1174.66, 0.043], [0.17, 1320, 0.019],
      [0.34, 1760, 0.020], [0.55, 2349.32, 0.012], [0.78, 1760, 0.009]]) {
      this.addTone(output, { start, frequency, gain, duration: 0.8, attack: 0.012,
        decay: 4.4, harmonics: [[1, 1], [2.006, 0.17], [3.97, 0.012]], vibrato: 0.0005 });
    }
    return this.finish(this.reverb(output));
  }

  renderClearHealing() {
    const output = new Float64Array(Math.round(1.85 * this.sampleRate));
    this.addAir(output, { duration: 1.18, gain: 0.075, center: 2000, swell: 0.055 });
    this.addTone(output, { frequency: 293.665, duration: 1.45, gain: 0.105,
      attack: 0.055, decay: 2.4, harmonics: [[1, 1], [2, 0.16]] });
    // 맑은 공명을 먼저 들려주고 높은 배음이 뒤따라 회복 시작을 더 뚜렷하게 알린다.
    this.addTone(output, { frequency: 587.33, duration: 1.58, gain: 0.15,
      attack: 0.016, decay: 3.5, harmonics: [[1, 1], [2.003, 0.28], [2.99, 0.035], [4.03, 0.018]] });
    this.addTone(output, { start: 0.075, frequency: 880, duration: 1.4, gain: 0.072,
      attack: 0.065, decay: 2.7, rise: 0.006, harmonics: [[1, 1], [2.004, 0.15]] });
    for (const [start, frequency, gain] of [[0.20, 1174.66, 0.046],
      [0.36, 1760, 0.027], [0.51, 2349.32, 0.016], [0.66, 2640, 0.007]]) {
      this.addTone(output, { start, frequency, gain, duration: 0.82, attack: 0.013,
        decay: 3.9, harmonics: [[1, 1], [2.001, 0.12]], vibrato: 0.0004 });
    }
    return this.finish(this.reverb(output));
  }

  finish(input) {
    let output = this.filter(this.filter(input, 'highpass', 85), 'lowpass', 6400);
    // 두 안은 같은 평균 음량으로 맞춰 음량 차이보다 질감을 비교하게 한다.
    const activeFrames = Math.round(1.4 * this.sampleRate);
    let energy = 0;
    for (let frame = 0; frame < activeFrames; frame++) energy += output[frame] ** 2;
    const targetRms = Math.pow(10, -20 / 20);
    const gain = targetRms / Math.sqrt(energy / activeFrames);
    for (let frame = 0; frame < output.length; frame++) {
      const time = frame / this.sampleRate;
      const remaining = (output.length - 1 - frame) / this.sampleRate;
      output[frame] *= gain * this.smoothStep(time / 0.006) * this.smoothStep(remaining / 0.12);
    }
    let peak = 0;
    for (const sample of output) peak = Math.max(peak, Math.abs(sample));
    if (peak > 0.80) output = output.map(sample => sample * 0.80 / peak);
    return output;
  }

  encodeWave(samples) {
    const buffer = Buffer.alloc(44 + samples.length * 2);
    buffer.write('RIFF', 0); buffer.writeUInt32LE(buffer.length - 8, 4);
    buffer.write('WAVEfmt ', 8); buffer.writeUInt32LE(16, 16);
    buffer.writeUInt16LE(1, 20); buffer.writeUInt16LE(1, 22);
    buffer.writeUInt32LE(this.sampleRate, 24); buffer.writeUInt32LE(this.sampleRate * 2, 28);
    buffer.writeUInt16LE(2, 32); buffer.writeUInt16LE(16, 34);
    buffer.write('data', 36); buffer.writeUInt32LE(samples.length * 2, 40);
    for (let frame = 0; frame < samples.length; frame++) {
      const dither = (this.random() - this.random()) / 65536;
      buffer.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round((samples[frame] + dither) * 32767))), 44 + frame * 2);
    }
    return buffer;
  }

  async writePreviews(directory) {
    await fs.mkdir(directory, { recursive: true });
    const variants = [
      { file: 'Preview_GourdHeal_A_Warm.wav', title: 'A · 온기 차오름',
        description: '부드러운 온기가 피어오르고, 은은한 빛의 잔향이 남는 치유음.',
        samples: this.renderWarmHealing() },
      { file: 'Preview_GourdHeal_B_Clear.wav', title: 'B · 맑은 생기',
        description: '맑은 공명과 작은 반짝임으로 회복 시작을 더 또렷하게 전하는 치유음.',
        samples: this.renderClearHealing() }
    ];
    const metrics = [];
    for (const variant of variants) {
      const wave = this.encodeWave(variant.samples);
      await fs.writeFile(path.join(directory, variant.file), wave);
      let peak = 0, energy = 0, maximumStep = 0;
      for (let frame = 0; frame < variant.samples.length; frame++) {
        const value = wave.readInt16LE(44 + frame * 2) / 32768;
        peak = Math.max(peak, Math.abs(value)); energy += value ** 2;
        if (frame) maximumStep = Math.max(maximumStep, Math.abs(value - wave.readInt16LE(42 + frame * 2) / 32768));
      }
      metrics.push({ file: variant.file, duration: variant.samples.length / this.sampleRate,
        sampleRate: this.sampleRate, channels: 1, bitDepth: 16,
        peakDbfs: +(20 * Math.log10(peak)).toFixed(2),
        rmsDbfs: +(20 * Math.log10(Math.sqrt(energy / variant.samples.length))).toFixed(2),
        maximumStep: +maximumStep.toFixed(4),
        firstSample: wave.readInt16LE(44), lastSample: wave.readInt16LE(wave.length - 2) });
      variant.uri = `data:audio/wav;base64,${wave.toString('base64')}`;
      variant.duration = variant.samples.length / this.sampleRate;
    }
    const original = await fs.readFile(path.resolve(directory, '../../Arts/Audio/MountainPass/Presentation/GourdRestore.wav'));
    const originalUri = `data:audio/wav;base64,${original.toString('base64')}`;
    await fs.writeFile(path.join(directory, 'GourdHealingPreview.html'), this.createHtml(variants, originalUri));
    await fs.writeFile(path.join(directory, 'PreviewMetrics.json'), JSON.stringify(metrics, null, 2) + '\n');
    return metrics;
  }

  createHtml(variants, originalUri) {
    return `<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>호리병 · 치유 사운드 미리듣기</title>
<style>
:root{color-scheme:dark;font-family:"Malgun Gothic",system-ui,sans-serif;background:#101817;color:#eaf2eb}
*{box-sizing:border-box}body{margin:0;padding:48px 24px;line-height:1.7}main{max-width:820px;margin:auto}
.eyebrow{letter-spacing:.17em;font-size:12px;color:#a5c3ac}h1{font-size:32px;font-weight:600;margin:12px 0}
.intro{color:#b4c9bb;margin:0 0 30px}.tag{display:inline-block;border:1px solid #466051;border-radius:20px;padding:3px 12px;font-size:12px;color:#c2dfcb}
.cards{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin:24px 0}.card{padding:24px;background:#192620;border:1px solid #344c3c;border-radius:18px}
.recommended{border-color:#7ba987}.label{color:#a9ccad;font-size:12px}.card h2{font-size:21px;margin:9px 0 8px}.card p{font-size:14px;color:#c0d0c4;min-height:72px;margin:0 0 18px}
audio{width:100%;height:42px}.detail{font-size:12px;color:#9bb3a2;margin-top:12px}.original{border-top:1px solid #34463b;padding-top:24px;margin-top:28px}.original audio{max-width:400px;display:block}
a{color:#bbddc3;text-underline-offset:4px;font-size:13px}.download{display:block;margin-top:15px}footer{color:#91ab9a;font-size:12px;margin-top:30px}
@media(max-width:650px){body{padding:26px 18px}.cards{grid-template-columns:1fr}h1{font-size:26px}.card p{min-height:0}}
</style></head><body><main>
<div class="eyebrow">CHEON / SOUND PREVIEW</div><h1>호리병에 담긴 치유의 기운</h1>
<p class="intro">기운이 피어오르고 생명력이 차오르는 순간을 소리로 만들었습니다.</p>
<span class="tag">미리듣기 전용 · 게임 미적용</span>
<div class="cards">${variants.map((variant, index) => `<article class="card ${index === 0 ? 'recommended' : ''}"><span class="label">${index === 0 ? '추천 · 은은한 회복감' : '비교안 · 선명한 회복감'}</span><h2>${variant.title}</h2><p>${variant.description}</p><audio controls preload="auto" aria-label="${variant.title} 미리듣기" src="${variant.uri}"></audio><div class="detail">${variant.duration.toFixed(2)}초 · WAV · 44.1 kHz / 16 bit / Mono</div><a class="download" href="${variant.uri}" download="${variant.file}">WAV 저장</a></article>`).join('')}</div>
<section class="original"><div class="label">현재 사운드 비교 · 1.55초</div><p>기존 호리병 효과음</p><audio controls preload="auto" aria-label="기존 호리병 효과음" src="${originalUri}"></audio></section>
<footer>두 후보는 비슷한 평균 음량으로 제작했습니다. 하나를 재생하면 다른 소리는 자동으로 멈춥니다.<br>미리듣기 파일만 Assets/Temp/GourdHealingAudio~/에 저장했습니다.</footer>
</main><script>const players=[...document.querySelectorAll('audio')];for(const player of players){player.volume=0.65;player.addEventListener('play',()=>{for(const other of players)if(other!==player){other.pause();other.currentTime=0}})}</script></body></html>`;
  }
}

const directory = path.dirname(fileURLToPath(import.meta.url));
export const previewMetrics = await new GourdHealingPreview().writePreviews(directory);
