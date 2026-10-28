import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

// 국립국악원 녹음의 음색을 살려 가공한다. 게임 에셋과 연결은 수정하지 않는다.
export class GugakHealingPreview {
  constructor(sampleRate = 44100) {
    this.sampleRate = sampleRate;
    this.directory = path.dirname(fileURLToPath(import.meta.url));
  }

  smoothStep(value) {
    const progress = Math.max(0, Math.min(1, value));
    return progress * progress * (3 - 2 * progress);
  }

  decodeWave(buffer) {
    if (buffer.toString('ascii', 0, 4) !== 'RIFF' || buffer.toString('ascii', 8, 12) !== 'WAVE') {
      throw new Error('올바른 WAV 원음이 아닙니다.');
    }
    let format, data;
    for (let offset = 12; offset + 8 <= buffer.length;) {
      const length = buffer.readUInt32LE(offset + 4);
      const name = buffer.toString('ascii', offset, offset + 4);
      if (name === 'fmt ') {
        const start = offset + 8;
        let encoding = buffer.readUInt16LE(start);
        if (encoding === 65534) encoding = buffer.readUInt16LE(start + 24);
        format = { encoding, channels: buffer.readUInt16LE(start + 2),
          rate: buffer.readUInt32LE(start + 4), block: buffer.readUInt16LE(start + 12),
          bits: buffer.readUInt16LE(start + 14) };
      }
      if (name === 'data') data = { start: offset + 8, length };
      offset += 8 + length + (length % 2);
    }
    if (!format || !data || data.start + data.length > buffer.length) throw new Error('WAV 다운로드가 완료되지 않았습니다.');
    const frameCount = Math.floor(data.length / format.block);
    const samples = new Float64Array(frameCount);
    const sampleBytes = format.bits / 8;
    for (let frame = 0; frame < frameCount; frame++) {
      for (let channel = 0; channel < format.channels; channel++) {
        const index = data.start + frame * format.block + channel * sampleBytes;
        let value;
        if (format.encoding === 3 && format.bits === 32) value = buffer.readFloatLE(index);
        else if (format.encoding === 1 && format.bits === 16) value = buffer.readInt16LE(index) / 32768;
        else if (format.encoding === 1 && format.bits === 24) value = buffer.readIntLE(index, 3) / 8388608;
        else if (format.encoding === 1 && format.bits === 32) value = buffer.readInt32LE(index) / 2147483648;
        else throw new Error(`지원하지 않는 WAV 형식: ${format.encoding}/${format.bits}`);
        samples[frame] += value / format.channels;
      }
    }
    return { samples, ...format, duration: frameCount / format.rate };
  }

  estimatePitch(samples, rate, start) {
    const downsample = Math.max(1, Math.floor(rate / 11025));
    const analysisRate = rate / downsample;
    const first = Math.round(start * rate);
    const count = Math.min(2048, Math.floor((samples.length - first) / downsample));
    if (count < 500) return null;
    const signal = Float64Array.from({ length: count }, (_, index) => {
      let sum = 0;
      for (let j = 0; j < downsample; j++) sum += samples[first + index * downsample + j];
      return sum / downsample;
    });
    const correlations = [];
    const minimumLag = Math.floor(analysisRate / 1600);
    const maximumLag = Math.ceil(analysisRate / 70);
    for (let lag = minimumLag; lag <= maximumLag; lag++) {
      let product = 0, firstEnergy = 0, secondEnergy = 0;
      for (let frame = 0; frame < count - maximumLag; frame++) {
        product += signal[frame] * signal[frame + lag];
        firstEnergy += signal[frame] ** 2; secondEnergy += signal[frame + lag] ** 2;
      }
      correlations.push({ lag, score: product / Math.max(1e-12, Math.sqrt(firstEnergy * secondEnergy)) });
    }
    const peaks = correlations.filter((entry, index) => index > 0 && index < correlations.length - 1
      && entry.score > correlations[index - 1].score && entry.score >= correlations[index + 1].score);
    const maximum = Math.max(...peaks.map(entry => entry.score));
    const best = peaks.find(entry => entry.score >= Math.max(0.6, maximum * 0.94));
    if (!best) return null;
    // 상관도 정점을 보간해 원래 연주의 음높이를 정확히 잡고 불필요한 이조를 줄인다.
    const index = best.lag - minimumLag;
    const a = correlations[index - 1].score, b = best.score, c = correlations[index + 1].score;
    const correction = 0.5 * (a - c) / (a - 2 * b + c);
    return { frequency: +(analysisRate / (best.lag + correction)).toFixed(2), correlation: +b.toFixed(3) };
  }

  analyze(source) {
    const window = Math.round(source.rate * 0.01);
    const envelope = [];
    for (let start = 0; start < source.samples.length; start += window) {
      let energy = 0;
      for (let frame = start; frame < Math.min(start + window, source.samples.length); frame++) {
        energy += source.samples[frame] ** 2;
      }
      envelope.push(Math.sqrt(energy / window));
    }
    const peak = Math.max(...envelope);
    const active = envelope.map(value => value > peak * 0.035);
    const regions = [];
    let first = null, last = 0;
    for (let frame = 0; frame <= active.length + 20; frame++) {
      if (active[frame]) { if (first === null) first = frame; last = frame; }
      else if (first !== null && frame - last >= 18) {
        const start = Math.max(0, (first - 2) * 0.01);
        const end = Math.min(source.duration, (last + 5) * 0.01);
        if (end - start >= 0.12) regions.push({ start: +start.toFixed(3), end: +end.toFixed(3),
          pitch: this.estimatePitch(source.samples, source.rate, start + Math.min(0.18, (end - start) / 3)) });
        first = null;
      }
    }
    return { duration: source.duration, rate: source.rate, channels: source.channels,
      bits: source.bits, envelopePeak: peak, regions };
  }

  async loadSources() {
    const sourceFiles = {
      gayageum: { file: 'sanjo_gayageum_sus_mid_03.wav', id: 2518, name: '산조가야금 기본음계 중' },
      daegeum: { file: 'jungak_deageum_scale_sus_25.wav', id: 2557, name: '정악대금 기본음' },
      jing: { file: 'Jing_2.wav', id: 2335, name: '징 약' }
    };
    this.sources = {};
    for (const [key, entry] of Object.entries(sourceFiles)) {
      const bytes = await fs.readFile(path.join(this.directory, 'GugakSource', entry.file));
      this.sources[key] = { ...this.decodeWave(bytes), ...entry,
        sha256: crypto.createHash('sha256').update(bytes).digest('hex') };
    }
    return Object.fromEntries(Object.entries(this.sources).map(([key, value]) => [key, this.analyze(value)]));
  }

  filter(input, frequency, highpass = false) {
    const omega = Math.PI * 2 * frequency / this.sampleRate;
    const cosine = Math.cos(omega), alpha = Math.sin(omega) / Math.sqrt(2);
    const denominator = 1 + alpha;
    const b0 = (highpass ? 1 + cosine : 1 - cosine) / (2 * denominator);
    const b1 = (highpass ? -(1 + cosine) : 1 - cosine) / denominator;
    const b2 = b0, a1 = -2 * cosine / denominator, a2 = (1 - alpha) / denominator;
    const output = new Float64Array(input.length);
    let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
    for (let frame = 0; frame < input.length; frame++) {
      const value = b0 * input[frame] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
      output[frame] = value;
      x2 = x1; x1 = input[frame]; y2 = y1; y1 = value;
    }
    return output;
  }

  addRecording(output, key, { sourceStart, duration, start = 0, gain = 1,
    speed = 1, attack = 0.012, release = 0.25, lowpass = 5600, swell = false }) {
    const source = this.sources[key];
    const frameCount = Math.min(Math.round(duration * this.sampleRate), output.length - Math.round(start * this.sampleRate));
    const clip = new Float64Array(frameCount);
    for (let frame = 0; frame < frameCount; frame++) {
      const sourcePosition = sourceStart * source.rate + frame * source.rate * speed / this.sampleRate;
      const index = Math.floor(sourcePosition), fraction = sourcePosition - index;
      if (index + 1 >= source.samples.length) break;
      clip[frame] = source.samples[index] * (1 - fraction) + source.samples[index + 1] * fraction;
    }
    const filtered = this.filter(this.filter(clip, 100, true), lowpass);
    let peak = 0;
    for (const sample of filtered) peak = Math.max(peak, Math.abs(sample));
    if (peak < 1e-5) throw new Error(`선택한 ${key} 원음 구간에 소리가 없습니다.`);
    const firstFrame = Math.round(start * this.sampleRate);
    for (let frame = 0; frame < frameCount; frame++) {
      const time = frame / this.sampleRate, remaining = (frameCount - 1 - frame) / this.sampleRate;
      let envelope = this.smoothStep(time / attack) * this.smoothStep(remaining / release);
      if (swell) envelope *= Math.sin(Math.PI * (0.15 + 0.7 * frame / frameCount)) ** 2;
      output[firstFrame + frame] += filtered[frame] * gain * envelope / peak;
    }
  }

  finish(input, wet = 0.24) {
    const output = Float64Array.from(input);
    const reflections = this.filter(input, 3100);
    // 직접 녹음의 여운을 흩어 배치한다. 반복되는 박자가 생기지 않게 반사 간격을 다르게 둔다.
    for (const [seconds, scale] of [[0.031, 0.64], [0.053, 0.51], [0.079, 0.42],
      [0.113, 0.36], [0.151, 0.30], [0.197, 0.24], [0.251, 0.18],
      [0.317, 0.13], [0.391, 0.085], [0.479, 0.05]]) {
      const delay = Math.round(seconds * this.sampleRate);
      for (let frame = delay; frame < output.length; frame++) {
        output[frame] += reflections[frame - delay] * wet * scale;
      }
    }
    const filtered = this.filter(this.filter(output, 85, true), 6300);
    const activeFrames = Math.min(filtered.length, Math.round(1.4 * this.sampleRate));
    let energy = 0, peak = 0;
    for (let frame = 0; frame < activeFrames; frame++) energy += filtered[frame] ** 2;
    let gain = 0.1 / Math.sqrt(energy / activeFrames);
    for (const value of filtered) peak = Math.max(peak, Math.abs(value));
    gain = Math.min(gain, 0.75 / peak);
    for (let frame = 0; frame < filtered.length; frame++) {
      filtered[frame] *= gain * this.smoothStep(frame / (this.sampleRate * 0.006))
        * this.smoothStep((filtered.length - 1 - frame) / (this.sampleRate * 0.14));
    }
    return filtered;
  }

  renderGayageumDaegeum(config) {
    const output = new Float64Array(Math.round(2.1 * this.sampleRate));
    this.addRecording(output, 'gayageum', { sourceStart: 45.94, duration: 1.5, gain: 0.50,
      speed: config.tonic / 296.53, attack: 0.018, release: 0.48, lowpass: 4600 });
    this.addRecording(output, 'gayageum', { sourceStart: 64.78, start: 0.20, duration: 1.28, gain: 0.23,
      speed: config.tonic * 1.5 / 446.25, attack: 0.018, release: 0.42, lowpass: 5100 });
    this.addRecording(output, 'gayageum', { sourceStart: 77.51, start: 0.42, duration: 1.14, gain: 0.105,
      speed: config.tonic * 2 / 592.66, attack: 0.020, release: 0.36 });
    this.addRecording(output, 'daegeum', { sourceStart: config.fluteStart, start: 0.065,
      duration: 1.48, gain: 0.17, speed: config.fluteSpeed, attack: 0.29, release: 0.56,
      lowpass: 3700, swell: true });
    return this.finish(output, 0.28);
  }

  renderGayageumJing(config) {
    const output = new Float64Array(Math.round(1.9 * this.sampleRate));
    this.addRecording(output, 'gayageum', { sourceStart: 45.94, duration: 1.22, gain: 0.48,
      speed: config.tonic / 296.53, attack: 0.015, release: 0.38, lowpass: 5000 });
    this.addRecording(output, 'gayageum', { sourceStart: 64.78, start: 0.15, duration: 1.15, gain: 0.26,
      speed: config.tonic * 1.5 / 446.25, attack: 0.016, release: 0.36 });
    this.addRecording(output, 'gayageum', { sourceStart: 77.51, start: 0.30, duration: 1.12, gain: 0.15,
      speed: config.tonic * 2 / 592.66, attack: 0.020, release: 0.32 });
    // 징의 타격은 지나간 부분을 사용해 강한 충격보다 조용한 공명만 남긴다.
    this.addRecording(output, 'jing', { sourceStart: config.jingStart, start: 0.04, duration: 1.30,
      gain: 0.065, attack: 0.24, release: 0.58, lowpass: 2300, swell: true });
    return this.finish(output, 0.32);
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
      buffer.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(samples[frame] * 32767))), 44 + frame * 2);
    }
    return buffer;
  }

  createHtml(variants, comparison) {
    return `<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>호리병 치유음 · 국악기 미리듣기</title><style>
:root{font-family:"Malgun Gothic",system-ui,sans-serif;color-scheme:dark;background:#101914;color:#e9efdf}*{box-sizing:border-box}body{margin:0;padding:40px 20px;line-height:1.75}main{max-width:850px;margin:auto}.eyebrow{font-size:12px;letter-spacing:.17em;color:#b9b786}h1{font-size:30px;font-weight:600;margin:10px 0}p{color:#bbc9b9}.tag{font-size:12px;border:1px solid #64734d;border-radius:18px;padding:4px 12px;display:inline-block}.cards{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin:26px 0}.card{background:#1b281d;border:1px solid #40563a;border-radius:17px;padding:24px}.card:first-child{border-color:#adb775}.label{color:#bdc78e;font-size:12px}h2{font-size:21px;margin:8px 0}.card p{font-size:14px;min-height:80px}audio{width:100%;height:42px}.detail{font-size:12px;color:#9bac96;margin-top:12px}a{color:#d0d7a6;text-underline-offset:4px}.download{display:block;font-size:13px;margin-top:14px}.comparison{border-top:1px solid #3a4b32;padding-top:20px;margin-top:25px}.comparison audio{max-width:400px;display:block}footer{font-size:12px;color:#9caf95;margin-top:28px}@media(max-width:640px){.cards{grid-template-columns:1fr}.card p{min-height:0}h1{font-size:25px}}
</style></head><body><main><div class="eyebrow">CHEON / GUGAK HEALING SOUND</div><h1>국악기로 피어나는 치유의 기운</h1>
<p>가야금의 짧은 울림 위에 대금의 숨결, 혹은 징의 여운을 더했습니다.</p><span class="tag">국립국악원 원음 편집 · 미리듣기 전용 · 게임 미적용</span>
<div class="cards">${variants.map((variant, index) => `<article class="card"><span class="label">${index === 0 ? '추천 · 부드러운 회복감' : '비교안 · 신비로운 공명'}</span><h2>${variant.title}</h2><p>${variant.description}</p><audio controls preload="auto" src="${variant.uri}" aria-label="${variant.title}"></audio><div class="detail">${variant.duration.toFixed(2)}초 · WAV / 44.1 kHz / 16 bit / Mono</div><a class="download" download="${variant.file}" href="${variant.uri}">WAV 저장</a></article>`).join('')}</div>
<section class="comparison"><span class="label">이전 합성음과 비교</span><p>A · 온기 차오름</p><audio controls preload="auto" aria-label="이전 합성음 A안" src="data:audio/wav;base64,${comparison.toString('base64')}"></audio></section>
<footer>출처: <a href="https://api.gugak.go.kr/digitaleum/front/monotone/list.do" target="_blank" rel="noreferrer">국립국악원 · 국악기 디지털 음원</a><br>공공누리 제1유형(출처표시). 가공: 개별 음 추출, 미세 음높이·음량 조정, 필터, 페이드, 레이어링, 잔향.<br>사용 원음: 산조가야금(2518), 정악대금(2557), 징 약(2335). 후원·제휴를 의미하지 않습니다.<br>하나를 재생하면 다른 소리는 멈춥니다.</footer>
</main><script>const players=[...document.querySelectorAll('audio')];for(const player of players){player.volume=0.65;player.addEventListener('play',()=>{for(const other of players)if(other!==player){other.pause();other.currentTime=0}})}</script></body></html>`;
  }

  async writePreviews(config) {
    if (!this.sources) await this.loadSources();
    const variants = [
      { file: 'Preview_GourdHeal_C_GayageumDaegeum.wav', title: 'C · 가야금과 대금',
        description: '가야금이 짧게 피어나고 대금의 부드러운 숨결이 이어집니다. 따뜻하게 생명력이 차오르는 느낌.',
        samples: this.renderGayageumDaegeum(config) },
      { file: 'Preview_GourdHeal_D_GayageumJing.wav', title: 'D · 가야금과 징',
        description: '가야금의 맑은 울림에 징의 작은 여운을 겹쳤습니다. 신비로운 기운이 퍼지는 느낌.',
        samples: this.renderGayageumJing(config) }
    ];
    const metrics = [];
    for (const variant of variants) {
      const buffer = this.encodeWave(variant.samples);
      await fs.writeFile(path.join(this.directory, variant.file), buffer);
      const decoded = this.decodeWave(buffer);
      let peak = 0, energy = 0, step = 0, clippedSamples = 0;
      for (let frame = 0; frame < decoded.samples.length; frame++) {
        const value = decoded.samples[frame];
        peak = Math.max(peak, Math.abs(value)); energy += value ** 2;
        if (Math.abs(value) >= 0.999) clippedSamples++;
        if (frame) step = Math.max(step, Math.abs(value - decoded.samples[frame - 1]));
      }
      metrics.push({ file: variant.file, duration: decoded.duration, sampleRate: decoded.rate,
        channels: decoded.channels, bitDepth: decoded.bits, peakDbfs: +(20 * Math.log10(peak)).toFixed(2),
        rmsDbfs: +(20 * Math.log10(Math.sqrt(energy / decoded.samples.length))).toFixed(2),
        maximumStep: +step.toFixed(4), clippedSamples, firstSample: decoded.samples[0], lastSample: decoded.samples.at(-1) });
      variant.uri = `data:audio/wav;base64,${buffer.toString('base64')}`;
      variant.duration = decoded.duration;
    }
    const comparison = await fs.readFile(path.join(this.directory, 'Preview_GourdHeal_A_Warm.wav'));
    await fs.writeFile(path.join(this.directory, 'GugakHealingPreview.html'), this.createHtml(variants, comparison));
    const manifest = { retrievedDate: '2026-10-28', publisher: '국립국악원',
      sourceUrl: 'https://api.gugak.go.kr/digitaleum/front/monotone/list.do',
      license: '공공누리 제1유형(출처표시)', sourceFiles: Object.values(this.sources).map(source => ({
        file: source.file, id: source.id, name: source.name, sha256: source.sha256 })),
      config, outputs: metrics };
    await fs.writeFile(path.join(this.directory, 'GugakPreviewManifest.json'), JSON.stringify(manifest, null, 2) + '\n');
    await fs.writeFile(path.join(this.directory, 'GugakPreviewCredits.txt'),
      '국립국악원 · 국악기 디지털 음원\n공공누리 제1유형(출처표시)\n' + manifest.sourceUrl + '\n\n'
      + manifest.sourceFiles.map(source => `${source.name} — ${source.file} (${source.id})`).join('\n')
      + '\n\n가공: 개별 음 추출, 미세 음높이·음량 조정, 필터, 페이드, 레이어링, 잔향.\n'
      + '원본 녹음을 바탕으로 편집한 게임용 미리듣기입니다. 국립국악원의 후원·제휴를 의미하지 않습니다.\n게임 에셋·연결은 변경하지 않았습니다.\n');
    return metrics;
  }
}
