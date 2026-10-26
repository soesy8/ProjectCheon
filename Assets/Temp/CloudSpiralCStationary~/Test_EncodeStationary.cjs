const path = require('path');
const sharp = require('C:/Users/SoEsy/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');

async function main() {
  const root = 'D:/mbcAcademy/ProjectCheon/Assets/Temp/CloudSpiralCStationary~';
  const frames = [];
  for (let index = 0; index < 40; index++) {
    frames.push(await sharp(path.join(root, `Test_Frame${String(index).padStart(2, '0')}.png`)).ensureAlpha().raw().toBuffer());
  }
  const delay = frames.map(() => 50);
  delay[delay.length - 1] = 700;
  await sharp(Buffer.concat(frames), {
    raw: { width: 768, height: 576 * frames.length, channels: 4, pageHeight: 576 }
  }).gif({ delay, loop: 0, effort: 7, colours: 256, dither: .3 })
    .toFile(path.join(root, 'Test_C_StationarySlash.gif'));
  process.stdout.write('Saved 40-frame slow-motion preview.\n');
}
main().catch(error => { process.stderr.write(error.stack); process.exitCode = 1; });
