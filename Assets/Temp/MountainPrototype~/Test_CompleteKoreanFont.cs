if(EditorApplication.isPlaying)throw new System.InvalidOperationException("편집 모드가 필요합니다.");
var path="Assets/Proto/MountainPass/Settings/MountainKorean.asset";
var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
var sourcePath="Assets/Proto/MountainPass/Settings/MountainKorean.ttf";
AssetDatabase.CopyAsset("Assets/Test/Cheon/Arts/Fonts/Test_CheonKorean.ttf",sourcePath);
var importer=AssetImporter.GetAtPath(sourcePath) as TrueTypeFontImporter;
if(importer!=null && !importer.includeFontData){importer.includeFontData=true;importer.SaveAndReimport();}
var source=AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
var generated=TMPro.TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,TMPro.AtlasPopulationMode.Dynamic,true);
if(generated==null)throw new System.InvalidOperationException("한국어 폰트 생성에 실패했습니다.");
var characters=string.Concat(System.IO.Directory.GetFiles("Assets/Proto/MountainPass/Scripts","*.cs").Select(System.IO.File.ReadAllText));
characters=new string(characters.Where(c=>!char.IsControl(c)).Distinct().ToArray());
generated.TryAddCharacters(characters,out string missing);
var oldAssets=AssetDatabase.LoadAllAssetsAtPath(path).Where(a=>a!=font).ToArray();
generated.name="MountainKorean";
EditorUtility.CopySerialized(generated,font);
font.material.name="M_MountainKorean";
AssetDatabase.AddObjectToAsset(font.material,font);
for(int i=0;i<font.atlasTextures.Length;i++) {font.atlasTextures[i].name="T_MountainKorean_Atlas"+i;AssetDatabase.AddObjectToAsset(font.atlasTextures[i],font);}
foreach(var asset in oldAssets){AssetDatabase.RemoveObjectFromAsset(asset);UnityEngine.Object.DestroyImmediate(asset,true);}
EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
UnityEngine.Object.DestroyImmediate(generated);
AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
return new { glyphs=font.glyphTable.Count, missing, atlases=font.atlasTextures.Select(t=>new {t.name,t.width,t.height}).ToArray(), hasTitle=font.HasCharacter('천'),hasEmber=font.HasCharacter('잔') };

