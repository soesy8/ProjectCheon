using System.Reflection;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Cheon.Testing
{
    public static class Test_CheonTextSettings
    {
        // TMP 공용 리소스가 없는 프로젝트에서도 테스트 폴더 밖으로 필수 리소스를 설치하지 않는다.
        // 기존 설정이 있으면 그대로 사용하고, 없을 때만 메모리에 임시 기본 설정을 제공한다.
        public static void Ensure(TMP_FontAsset font = null)
        {
            FieldInfo field = typeof(TMP_Settings).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) return;
            if (!(field.GetValue(null) is TMP_Settings))
            {
                TMP_Settings existing = Resources.Load<TMP_Settings>("TMP Settings");
                if (existing != null) field.SetValue(null, existing);
                else
                {
                    var settings = ScriptableObject.CreateInstance<TMP_Settings>();
                    field.SetValue(null, settings);
                    TMP_Settings.useModernHangulLineBreakingRules = true;
                    var rules = new TMP_Settings.LineBreakingTable
                    {
                        leadingCharacters = new HashSet<uint> { ')', ']', '}', '.', ',', '!', '?', ':', ';' },
                        followingCharacters = new HashSet<uint> { '(', '[', '{' }
                    };
                    typeof(TMP_Settings).GetField("m_linebreakingRules", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(settings, rules);
                }
            }
            if (font != null && TMP_Settings.defaultFontAsset == null) TMP_Settings.defaultFontAsset = font;
        }
    }
}
