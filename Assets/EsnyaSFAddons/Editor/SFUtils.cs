using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace EsnyaSFAddons
{
    public static class SFUtils
    {
        public static IEnumerable<FieldInfo> ListPublicVariables(Type type)
        {
            return type
                .GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .Where(field => field.GetCustomAttribute<NonSerializedAttribute>() == null);
        }

        public static MethodInfo[] ListCustomEvents(Type type)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        }

        public static bool IsExtention(Type type)
        {
            return ListCustomEvents(type).Any(m => m.Name.StartsWith("SFEXT_"));
        }

        public static bool IsDFUNC(Type type)
        {
            return ListCustomEvents(type).Any(m => m.Name.StartsWith("DFUNC_"));
        }

        public static UdonSharpBehaviour GetNearestController(GameObject o)
        {
            var controller = o.GetUdonSharpComponentInParent(typeof(SAV_PassengerFunctionsController)) ?? o.GetUdonSharpComponentInParent(typeof(SaccEntity));
            if (controller is SAV_PassengerFunctionsController && controller.gameObject == o) return o.GetUdonSharpComponentInParent(typeof(SaccEntity));
            return controller;
        }

        public static bool IsChildExtention(UdonSharpBehaviour controller, UdonSharpBehaviour extention)
        {
            return GetNearestController(extention.gameObject) == controller;
        }

        public static IEnumerable<UdonSharpBehaviour> FindExtentions(UdonSharpBehaviour root)
        {
            return root.GetUdonSharpComponentsInChildren<UdonSharpBehaviour>(true).Where(udon => udon.gameObject != root && IsExtention(udon.GetType()) && !IsDFUNC(udon.GetType()) && IsChildExtention(root, udon));
        }

        public static IEnumerable<UdonSharpBehaviour> FindDFUNCs(UdonSharpBehaviour root)
        {
            return root.GetUdonSharpComponentsInChildren<UdonSharpBehaviour>(true).Where(udon => udon.gameObject != root && IsDFUNC(udon.GetType()) && IsChildExtention(root, udon));
        }

        public static void SetObjectArrayProperty<T>(SerializedProperty property, IEnumerable<T> enumerable) where T : UnityEngine.Object
        {
            var array = enumerable.ToArray();
            property.arraySize = array.Length;

            for (var i = 0; i < array.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
            }
        }

        public static void UndoRecordUdonSharpBehaviour(UdonSharpBehaviour udonSharpBehaviour, string name)
        {
            Undo.RecordObject(udonSharpBehaviour, name);
        }

        public static bool ValidateReference<T>(UdonSharpBehaviour extention, string variableName, T expectedValue, MessageType messageType, bool forceFix = false) where T : class
        {
            if (expectedValue == null || extention.GetProgramVariable(variableName) != null) return false;

            if (forceFix || ESFAUI.HelpBoxWithAutoFix($"{extention}.{variableName} is not set.", messageType))
            {
                UndoRecordUdonSharpBehaviour(extention, "Auto Fix");
                extention.SetProgramVariable(variableName, expectedValue);
                return true;
            }

            return false;
        }

        public static void AlignMFDFunctions(this UdonSharpBehaviour entity, VRC_Pickup.PickupHand side)
        {
            var parent = (entity as SaccEntity)?.InVehicleOnly?.transform ?? entity.transform;
            var display = FindByName(parent, $"StickDisplay{side.ToString()[0]}")?.transform;
            if (!display) return;

            var functions = Enumerable
                .Range(0, display.childCount)
                .Select(display.GetChild)
                .Where(t => t.gameObject.name.StartsWith("MFD_"))
                .Select((transform, index) => (transform, index))
                .ToArray();

            var count = functions.Length;
            var dialFunctions = (side == VRC_Pickup.PickupHand.Left ? entity.GetProgramVariable(nameof(SaccEntity.Dial_Functions_L)) : entity.GetProgramVariable(nameof(SaccEntity.Dial_Functions_R))) as UdonSharpBehaviour[];
            foreach (var (transform, index) in functions)
            {
                try
                {
                    var localRotation = Quaternion.AngleAxis(360.0f * index / count, Vector3.back);
                    var localPosition = localRotation * Vector3.up * 0.14f;

                    Undo.RecordObject(transform, "Align MFD Function");
                    transform.localPosition = localPosition;
                    transform.localScale = Vector3.one;

                    var dialFunction = dialFunctions != null && index < dialFunctions.Length ? dialFunctions[index] : null;

                    var displayHighlighter = transform.Find("MFD_display_funcon")?.gameObject;
                    if (displayHighlighter)
                    {
                        Undo.RecordObject(displayHighlighter.transform, "Align MFD Function");
                        displayHighlighter.transform.position = transform.parent.position;
                        displayHighlighter.transform.localRotation = localRotation;

                        if ((UnityEngine.Object)dialFunction?.GetProgramVariable("Dial_Funcon") != displayHighlighter)
                        {
                            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(dialFunction);
                            Undo.RecordObject(udon, "Align MFD Function");
                            dialFunction.SetProgramVariable("Dial_Funcon", displayHighlighter);
                            dialFunction.ApplyProxyModifications();
                            EditorUtility.SetDirty(udon);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            var background = display.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(f => f.sharedMesh && f.sharedMesh.name.StartsWith("StickDisplay") && char.IsDigit(f.sharedMesh.name.Last()) || f.sharedMesh.name == "StickDisplay");
            if (background)
            {
                var expectedName = count == 8 ? "StkickDisplay" : $"StickDisplay{count}";
                var expectedMesh = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(background.sharedMesh)).Select(o => o as Mesh).FirstOrDefault(m => m && m.name == expectedName);
                if (expectedMesh && background.sharedMesh != expectedMesh)
                {
                    Undo.RecordObject(background, "Align MFD Function");
                    background.sharedMesh = expectedMesh;
                    EditorUtility.SetDirty(background);
                }
            }
        }

        public static IEnumerable<GameObject> ListByName(this Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).OrderBy(t => t.GetHierarchyPath().Count(c => c == '/')).Select(t => t.gameObject).Where(o => o.name == name);
        }

        public static GameObject FindByName(this Transform root, string name)
        {
            return ListByName(root, name).FirstOrDefault();
        }
    }
}
