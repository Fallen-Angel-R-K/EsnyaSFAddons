using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace EsnyaAircraftAssets
{
    [CustomEditor(typeof(SaccEntity))]
    public class SaccEntityEditor : Editor
    {
        private static void ValidationGUI(SaccEntity entity)
        {
            EditorGUILayout.LabelField("SaccEntity Validator");
            var fixAll = GUILayout.Button("Fix All");

            var extentions = SFUtils.FindExtentions(entity);
            var dfuncs = SFUtils.FindDFUNCs(entity);
            var seats = entity.GetUdonSharpComponentsInChildren<SaccVehicleSeat>(true);
            var animator = entity.GetComponent<Animator>();
            var airVehicle = extentions.FirstOrDefault(e => e is SaccAirVehicle) as SaccAirVehicle;
            var savSoundController = extentions.FirstOrDefault(e => e is SAV_SoundController);

            var others = entity.GetUdonSharpComponentsInChildren<SaccResupplyTrigger>(true).Select(t => t as UdonSharpBehaviour)
                .Concat(entity.GetUdonSharpComponentsInChildren<SAV_AAMController>(true))
                .Concat(entity.GetUdonSharpComponentsInChildren<SAV_AGMController>(true));

            foreach (var extention in extentions.Concat(dfuncs).Concat(seats).Concat(others))
            {
                var isDirty = false;

                var fields = SFUtils.ListPublicVariables(extention.GetType());
                foreach (var field in fields)
                {
                    var value = extention.GetProgramVariable(field.Name);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (value == null)
                        {
                            if (field.FieldType == typeof(SaccEntity))
                            {
                                isDirty = isDirty || SFUtils.ValidateReference(extention, field.Name, entity, MessageType.Warning, fixAll);
                            }
                            else if (field.FieldType == typeof(Animator))
                            {
                                isDirty = isDirty || SFUtils.ValidateReference(extention, field.Name, animator, MessageType.Info, fixAll);
                            }
                            else if (field.FieldType == typeof(UdonSharpBehaviour))
                            {
                                if (field.Name == "SAVControl") isDirty = isDirty || SFUtils.ValidateReference(extention, field.Name, airVehicle, MessageType.Warning, fixAll);
                                else if (field.Name == "SoundControl") isDirty = isDirty || SFUtils.ValidateReference(extention, field.Name, savSoundController, MessageType.Warning, fixAll);
                            }
                            else if (extention is SaccAirVehicle && field.Name == "VehicleMesh" || extention is SAV_SyncScript && field.Name == "VehicleTransform")
                            {
                                isDirty = isDirty || SFUtils.ValidateReference(extention, field.Name, entity.transform, MessageType.Warning, fixAll);
                            }
                        }
                    }
                }

                if (isDirty)
                {
                    extention.ApplyProxyModifications();
                    EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(extention));
                }
            }

            if (airVehicle && airVehicle.VehicleMesh && airVehicle.VehicleMesh.gameObject.layer != LayerMask.NameToLayer("Walkthrough") && ESFAUI.HelpBoxWithAutoFix($"VehicleMesh must be on the layer Walkthrough.", MessageType.Error))
            {
                Undo.RecordObject(airVehicle.VehicleMesh.gameObject, "Auto Fix");
                airVehicle.VehicleMesh.gameObject.layer = LayerMask.NameToLayer("Walkthrough");
            }

            if (entity.InVehicleOnly != null && entity.InVehicleOnly.activeSelf && ESFAUI.HelpBoxWithAutoFix($"InVehicleOnly should be deactivated.", MessageType.Warning))
            {
                Undo.RecordObject(entity.InVehicleOnly, "Auto Fix");
                entity.InVehicleOnly.SetActive(false);
            }
        }

        private void OnDisable()
        {
            DisableAllPreview();
        }

        private Dictionary<string, bool> previewStatus = new Dictionary<string, bool>();
        private void SetPreview(SerializedProperty property, bool value)
        {
            var gameObject = property.objectReferenceValue as GameObject;
            if (!gameObject) return;

            var key = property.propertyPath;
            previewStatus.TryGetValue(key, out var currentState);

            if (currentState == value) return;

            Undo.RecordObject(gameObject, "Preview");
            previewStatus[key] = value;
            gameObject.SetActive(value);
        }
        private void DisableAllPreview()
        {
            foreach (var path in previewStatus.Keys)
            {
                SetPreview(serializedObject.FindProperty(path), false);
            }
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            var entity = target as SaccEntity;

            serializedObject.Update();

            ValidationGUI(entity);

            var property = serializedObject.GetIterator();
            property.NextVisible(true);

            while (property.NextVisible(false))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(property, true);
                    if (property.name == nameof(SaccEntity.ExtensionUdonBehaviours))
                    {
                        if (ESFAUI.MiniButton("Find")) SFUtils.SetObjectArrayProperty(property, SFUtils.FindExtentions(entity));
                    }
                    else if (property.name == nameof(SaccEntity.Dial_Functions_L))
                    {
                        if (ESFAUI.MiniButton("Find")) SFUtils.SetObjectArrayProperty(property, SFUtils.FindDFUNCs(entity).Where(dfunc => dfunc.transform.parent.gameObject.name.EndsWith("L")));
                        if (ESFAUI.MiniButton("Align")) SFUtils.AlignMFDFunctions(entity, VRC_Pickup.PickupHand.Left);
                    }
                    else if (property.name == nameof(SaccEntity.Dial_Functions_R))
                    {
                        if (ESFAUI.MiniButton("Find")) SFUtils.SetObjectArrayProperty(property, SFUtils.FindDFUNCs(entity).Where(dfunc => dfunc.transform.parent.gameObject.name.EndsWith("R")));
                        if (ESFAUI.MiniButton("Align")) SFUtils.AlignMFDFunctions(entity, VRC_Pickup.PickupHand.Right);
                    }
                    else if (property.name == nameof(SaccEntity.InVehicleOnly) || property.name == nameof(SaccEntity.HoldingOnly))
                    {
                        if (ESFAUI.MiniButton("Preview")) SetPreview(property, true);
                        if (ESFAUI.MiniButton("Find")) property.objectReferenceValue = entity.transform.FindByName(property.name);
                    }
                    else if (property.name == nameof(SaccEntity.CenterOfMass) || property.name == nameof(SaccEntity.LStickDisplayHighlighter) || property.name == nameof(SaccEntity.RStickDisplayHighlighter))
                    {
                        if (ESFAUI.MiniButton("Find")) property.objectReferenceValue = entity.transform.FindByName(property.name)?.transform;
                    }
                    else if (property.name == nameof(SaccEntity.SwitchFunctionSound))
                    {
                        if (ESFAUI.MiniButton("Find")) property.objectReferenceValue = entity.transform.ListByName(property.name).Select(o => o.GetComponent<AudioSource>()).FirstOrDefault();
                    }
                    else if (property.name == nameof(SaccEntity.DisableAfter10Seconds))
                    {
                        if (ESFAUI.MiniButton("Find")) SFUtils.SetObjectArrayProperty(property, entity.transform.ListByName(property.name));
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
