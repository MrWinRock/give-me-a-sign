#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// Original code from 'EditorCools': https://github.com/datsfain/EditorCools
    /// </summary>

    public class ButtonDrawer
    {
        private readonly List<IGrouping<string, InspectorButton>> ButtonGroups;

        public ButtonDrawer(object target)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods = target.GetType().GetMethods(flags);
            List<InspectorButton> buttons = new List<InspectorButton>();
            int rowNumber = 0;

            foreach (MethodInfo method in methods)
            {
                ButtonAttribute buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
                if (buttonAttribute == null)
                {
                    continue;
                }

                buttons.Add(new InspectorButton(method, buttonAttribute));
            }

            ButtonGroups = buttons.GroupBy(button =>
            {
                ButtonAttribute attribute = button.ButtonAttribute;
                if (attribute.Row == "")
                {
                    return $"__{rowNumber++}";
                }

                return attribute.Row;
            }).ToList();
        }

        public void DrawButtons(IEnumerable<object> targets)
        {
            foreach (IGrouping<string, InspectorButton> buttonGroup in ButtonGroups)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (InspectorButton button in buttonGroup)
                    {
                        button.Draw(targets);
                    }
                }
            }
        }
        
    } // class end
} 
#endif