const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/SoEsy/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');

async function main() {
  const project = 'D:/mbcAcademy/ProjectCheon';
  const root = path.join(project, 'Assets/Temp/CloudSpiralDesign');
  const frames = [];
  for (let index = 0; index < 25; index++) {
    const file = path.join(root, `Preview_${String(index).padStart(2, '0')}.png`);
    const data = await sharp(file).ensureAlpha().raw().toBuffer();
    frames.push(data);
  }
  const delay = frames.map(() => 50);
  delay[delay.length - 1] = 500;
  await sharp(Buffer.concat(frames), {
    raw: { width: 1024, height: 768 * frames.length, channels: 4, pageHeight: 768 }
  }).gif({ delay, loop: 0, effort: 7, colours: 256, dither: 0.35 }).toFile(path.join(root, 'CloudSpiralMotion.gif'));
  process.stdout.write('Encoded native Unity particle preview: 25 frames, half-speed.\n');
}
main().catch(error => { process.stderr.write(error.stack); process.exitCode = 1; });
