// Sous-lot 9.3 : bouchons minimaux de l'API Unity (signatures seulement, aucun comportement), pour compiler en CI les scripts du
// prototype (unity/Assets/EdS) en C# 9 hors d'Unity. Ils détectent les erreurs de C# (noms, types, syntaxe) mais pas un écart avec la
// vraie API d'Unity : seul l'essai dans l'éditeur le fait. Un script qui utilise une API absente d'ici demande d'y ajouter son bouchon.
#pragma warning disable
using System;
namespace UnityEngine
{
    public class Object { public string name { get; set; } public static void Destroy(Object o) { } public static T Instantiate<T>(T original) where T : Object => original; public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => original; public static T FindFirstObjectByType<T>() where T : Object => default; public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => null; public static void DestroyImmediate(Object o) { } public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public Transform transform => null; public GameObject gameObject => null; public T GetComponent<T>() => default; public T GetComponentInChildren<T>() => default; public T[] GetComponentsInChildren<T>() => null; }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => default; }
    public sealed class GameObject : Object { public GameObject(string n) { } public Transform transform => null; public string tag { get; set; } public T AddComponent<T>() where T : Component => default; public T GetComponent<T>() => default; public T GetComponentInChildren<T>() => default; public T[] GetComponentsInChildren<T>() => null; public void SetActive(bool b) { } public bool activeSelf => false; public static GameObject CreatePrimitive(PrimitiveType t) => null; }
    public enum FindObjectsSortMode { None, InstanceID }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public class Transform : Component { public Vector3 position { get; set; } public Quaternion rotation { get; set; } public Vector3 localPosition { get; set; } public Quaternion localRotation { get; set; } public Vector3 localScale { get; set; } public void SetParent(Transform p, bool w) { } public void LookAt(Vector3 v) { } }
    public class Renderer : Component { public Material sharedMaterial { get; set; } public Material material { get; set; } public bool enabled { get; set; } public Bounds bounds => default; public Material[] materials { get; set; } }
    public struct Bounds { public Vector3 min => default; public Vector3 max => default; public Vector3 size => default; public Vector3 center => default; public void Encapsulate(Bounds b) { } }
    public enum WrapMode { Default = 0, Once = 1, Loop = 2, PingPong = 4, ClampForever = 8 }
    public sealed class AnimationState { public string name { get; set; } public WrapMode wrapMode { get; set; } public float length => 0; }
    public sealed class AnimationClip : Object { }
    public sealed class Animation : Behaviour, System.Collections.IEnumerable { public System.Collections.IEnumerator GetEnumerator() => null; public void AddClip(AnimationClip clip, string newName) { } public AnimationClip GetClip(string name) => null; public AnimationState this[string name] => null; public void Rewind(string name) { } public void CrossFade(string animation, float fadeLength) { } public bool Play(string animation) => false; public void Stop() { } }
    public sealed class AudioClip : Object { }
    public sealed class AudioSource : Behaviour { public void PlayOneShot(AudioClip clip, float volumeScale) { } }
    public sealed class AudioListener : Behaviour { }
    public sealed class MeshRenderer : Renderer { }
    public sealed class SkinnedMeshRenderer : Renderer { public bool updateWhenOffscreen { get; set; } }
    public sealed class MeshFilter : Component { public Mesh mesh { get; set; } }
    public sealed class Mesh : Object { public Mesh() { } public Vector3[] vertices { get; set; } public int[] triangles { get; set; } public void Clear() { } public void RecalculateNormals() { } public void RecalculateBounds() { } }
    public static class PlayerPrefs { public static float GetFloat(string key, float defaultValue) => defaultValue; public static void SetFloat(string key, float value) { } public static void Save() { } }
    public class Collider : Component { }
    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public sealed class Light : Behaviour { public LightType type { get; set; } public float intensity { get; set; } public LightShadows shadows { get; set; } public Color color { get; set; } }
    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }
    public static class RenderSettings { public static Material skybox { get; set; } public static UnityEngine.Rendering.AmbientMode ambientMode { get; set; } public static Color ambientSkyColor { get; set; } public static Color ambientEquatorColor { get; set; } public static Color ambientGroundColor { get; set; } public static bool fog { get; set; } public static FogMode fogMode { get; set; } public static Color fogColor { get; set; } public static float fogStartDistance { get; set; } public static float fogEndDistance { get; set; } }
    public sealed class TerrainLayer : Object { public TerrainLayer() { } public Texture2D diffuseTexture { get; set; } public Texture2D normalMapTexture { get; set; } public Vector2 tileSize { get; set; } }
    public sealed class TerrainData : Object { public TerrainData() { } public int heightmapResolution { get; set; } public int alphamapResolution { get; set; } public Vector3 size { get; set; } public TerrainLayer[] terrainLayers { get; set; } public void SetAlphamaps(int x, int y, float[,,] map) { } }
    public sealed class Terrain : Behaviour { public TerrainData terrainData { get; set; } public Material materialTemplate { get; set; } public float basemapDistance { get; set; } public float heightmapPixelError { get; set; } public bool drawTreesAndFoliage { get; set; } }
    [AttributeUsage(AttributeTargets.Field)] public class PropertyAttribute : Attribute { }
    public sealed class HeaderAttribute : PropertyAttribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class ContextMenu : Attribute { public ContextMenu(string itemName) { } }
    public sealed class LODGroup : Component { public bool enabled { get; set; } }
    public sealed class TooltipAttribute : PropertyAttribute { public TooltipAttribute(string tooltip) { } }
    public sealed class RangeAttribute : PropertyAttribute { public RangeAttribute(float min, float max) { } }
    public enum CameraClearFlags { Skybox = 1, SolidColor = 2 }
    public sealed class Camera : Behaviour { public static Camera main => null; public float fieldOfView { get; set; } public float nearClipPlane { get; set; } public float farClipPlane { get; set; } public CameraClearFlags clearFlags { get; set; } public Color backgroundColor { get; set; } public Vector3 WorldToScreenPoint(Vector3 p) => default; }
    public struct Color32 { public byte r, g, b, a; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b) : this(r, g, b, 1) { } public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => default; public static Color black => default; public static Color Lerp(Color a, Color b, float t) => a; }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; public float magnitude => 0; public Vector2 normalized => default; public static float Distance(Vector2 a, Vector2 b) => 0; public static Vector2 operator +(Vector2 a, Vector2 b) => a; public static Vector2 operator -(Vector2 a, Vector2 b) => a; public static Vector2 operator *(Vector2 a, float f) => a; public static Vector2 operator /(Vector2 a, float f) => a; }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public int fingerId => 0; public Vector2 position => default; public TouchPhase phase => default; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero => default; public static Vector3 up => default; public static Vector3 forward => default; public float magnitude => 0; public float sqrMagnitude => 0; public Vector3 normalized => this; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a; public static Vector3 Cross(Vector3 a, Vector3 b) => a; public static float Dot(Vector3 a, Vector3 b) => 0; public static Vector3 operator /(Vector3 a, float d) => a; public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a; public static Vector3 operator *(Vector3 a, float d) => a; public static Vector3 operator *(float d, Vector3 a) => a; }
    public struct Quaternion { public static Quaternion identity => default; public static Quaternion Euler(float x, float y, float z) => default; public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a; public static Quaternion LookRotation(Vector3 f) => default; public Vector3 eulerAngles => default; public static Vector3 operator *(Quaternion q, Vector3 v) => v; public static Quaternion operator *(Quaternion a, Quaternion b) => a; }
    public struct Rect { public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } public float x, y, width, height; }
    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) => default; }
    public static class Mathf { public static float Clamp(float v, float a, float b) => v; public static float Clamp01(float v) => v; public static float Lerp(float a, float b, float t) => a; public static float LerpAngle(float a, float b, float t) => a; public static float Exp(float p) => p; public static float Sin(float f) => f; public static float Cos(float f) => f; public static float Atan2(float y, float x) => y; public static float Abs(float f) => f; public static float Min(float a, float b) => a; public static float Max(float a, float b) => a; public static float MoveTowards(float current, float target, float maxDelta) => current; public const float PI = 3.14159274f; public const float Deg2Rad = 0.0174532924f; public const float Rad2Deg = 57.29578f; }
    public enum KeyCode { None, Backspace, Tab, Escape, Space, Return, KeypadEnter, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9, UpArrow, DownArrow, RightArrow, LeftArrow, F1, F2, F3, F4, F5, F6, F8, A, C, D, E, F, G, H, I, J, K, L, M, N, P, Q, R, S, T, W, Z, Semicolon, LeftShift, RightShift }
    public static class Input { public static bool GetKey(KeyCode k) => false; public static bool GetKeyDown(KeyCode k) => false; public static bool GetMouseButton(int b) => false; public static bool GetMouseButtonDown(int b) => false; public static bool GetMouseButtonUp(int b) => false; public static Vector3 mousePosition => default; public static float GetAxis(string a) => 0; public static Vector2 mouseScrollDelta => default; public static bool touchSupported => false; public static int touchCount => 0; public static Touch GetTouch(int i) => default; }
    public static class Time { public static float time => 0; public static float deltaTime => 0; }
    public enum RuntimePlatform { WindowsEditor, WindowsPlayer, Android, IPhonePlayer }
    public static class Application { public static bool isEditor => false; public static string dataPath => ""; public static string streamingAssetsPath => ""; public static string persistentDataPath => ""; public static bool isMobilePlatform => false; public static RuntimePlatform platform => default; public static int targetFrameRate { get; set; } }
    public class AsyncOperation { }
    public static class SleepTimeout { public const int NeverSleep = -1; }
    public enum ScreenOrientation { Portrait = 1, LandscapeLeft = 3, AutoRotation = 5 }
    public enum ShadowQuality { Disable, HardOnly, All }
    public static class Debug { public static void Log(object m) { } public static void LogWarning(object m) { } public static void LogException(Exception e) { } }
    public sealed class Shader : Object { public static Shader Find(string n) => null; }
    public static class Resources { public static T Load<T>(string path) where T : Object => default; }
    public class Material : Object { public Material(Shader s) { } public Material(Material source) { } public bool HasProperty(string n) => false; public void SetColor(string n, Color c) { } public Color GetColor(string n) => default; public void SetTexture(string n, Texture t) { } public void SetFloat(string n, float f) { } public void EnableKeyword(string k) { } public int renderQueue { get; set; } }
    public class Texture : Object { public int width => 0; public int height => 0; }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public sealed class Texture2D : Texture { public Texture2D(int width, int height, TextureFormat format, bool mipChain) { } public static Texture2D whiteTexture => null; public FilterMode filterMode { get; set; } public void LoadRawTextureData(byte[] data) { } public void Apply() { } public Color32[] GetPixels32() => null; public void SetPixels32(Color32[] colors) { } public byte[] EncodeToPNG() => null; }
    public enum TextureFormat { RGBA32 = 4 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public enum TextAnchor { UpperLeft, MiddleCenter }
    public enum FontStyle { Normal, Bold }
    public sealed class GUIStyleState { public Color textColor { get; set; } }
    public sealed class GUIStyle { public GUIStyle(GUIStyle other) { } public int fontSize { get; set; } public bool richText { get; set; } public TextAnchor alignment { get; set; } public FontStyle fontStyle { get; set; } public GUIStyleState normal { get; set; } }
    public sealed class GUISkin : Object { public GUIStyle label => null; public GUIStyle button => null; public GUIStyle textField => null; }
    public static class GUI { public static GUISkin skin => null; public static Color color { get; set; } public static Matrix4x4 matrix { get; set; } public static void DrawTexture(Rect r, Texture t) { } public static void Label(Rect r, string t, GUIStyle s) { } public static bool Button(Rect r, string t, GUIStyle s) => false; public static string TextField(Rect r, string t, int maxLength, GUIStyle s) => t; }
    public static class GUIUtility { public static int keyboardControl { get; set; } }
    public static class Screen { public static int width => 0; public static int height => 0; public static int sleepTimeout { get; set; } public static bool autorotateToPortrait { get; set; } public static bool autorotateToPortraitUpsideDown { get; set; } public static bool autorotateToLandscapeLeft { get; set; } public static bool autorotateToLandscapeRight { get; set; } public static ScreenOrientation orientation { get; set; } }
    public static class QualitySettings { public static UnityEngine.Rendering.RenderPipelineAsset renderPipeline { get; set; } public static ShadowQuality shadows { get; set; } public static int antiAliasing { get; set; } }
}
namespace UnityEngine.Networking
{
    public sealed class DownloadHandler { public byte[] data => null; public string text => null; }
    public sealed class UnityWebRequestAsyncOperation : UnityEngine.AsyncOperation { }
    public sealed class UnityWebRequest : IDisposable { public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError } public static UnityWebRequest Get(string uri) => null; public UnityWebRequestAsyncOperation SendWebRequest() => null; public Result result => default; public string error => null; public DownloadHandler downloadHandler => null; public void Dispose() { } }
}
namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public class VolumeComponent : ScriptableObject { public bool active; }
    public class VolumeParameter<T> { public T value { get; set; } public bool overrideState { get; set; } public void Override(T x) { } }
    public class FloatParameter : VolumeParameter<float> { }
    public sealed class ClampedFloatParameter : FloatParameter { }
    public sealed class MinFloatParameter : FloatParameter { }
    public sealed class VolumeProfile : ScriptableObject { public T Add<T>(bool overrides = false) where T : VolumeComponent => default; }
    public sealed class Volume : MonoBehaviour { public bool isGlobal { get; set; } public float priority { get; set; } public VolumeProfile sharedProfile { get; set; } }
    public abstract class RenderPipelineAsset : ScriptableObject { }
    public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline => null; public static RenderPipelineAsset defaultRenderPipeline { get; set; } }
}
namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererData : ScriptableObject { }
    public sealed class UniversalRendererData : ScriptableRendererData { public PostProcessData postProcessData; }
    public sealed class PostProcessData : ScriptableObject { }
    public enum TonemappingMode { None, Neutral, ACES }
    public sealed class TonemappingModeParameter : UnityEngine.Rendering.VolumeParameter<TonemappingMode> { }
    public sealed class Tonemapping : UnityEngine.Rendering.VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class ColorAdjustments : UnityEngine.Rendering.VolumeComponent { public UnityEngine.Rendering.FloatParameter postExposure; public UnityEngine.Rendering.ClampedFloatParameter contrast; public UnityEngine.Rendering.ClampedFloatParameter saturation; }
    public sealed class Bloom : UnityEngine.Rendering.VolumeComponent { public UnityEngine.Rendering.MinFloatParameter threshold; public UnityEngine.Rendering.MinFloatParameter intensity; }
    public sealed class Vignette : UnityEngine.Rendering.VolumeComponent { public UnityEngine.Rendering.ClampedFloatParameter intensity; }
    public sealed class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing { get; set; } }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }
    public sealed class UniversalRenderPipelineAsset : RenderPipelineAsset { public static UniversalRenderPipelineAsset Create(ScriptableRendererData d = null) => null; }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem : Attribute { public MenuItem(string p) { } }
    public static class EditorApplication { public delegate void CallbackFunction(); public static CallbackFunction delayCall; public static bool isPlayingOrWillChangePlaymode => false; }
    public sealed class SceneAsset : UnityEngine.Object { }
    public static class BuildPipeline { public static bool isBuildingPlayer => false; }
    public sealed class EditorBuildSettingsScene { public EditorBuildSettingsScene(string path, bool enabled) { } public string path => ""; public bool enabled => false; }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } }
    public class AssetImporter : UnityEngine.Object { public struct SourceAssetIdentifier { public SourceAssetIdentifier(UnityEngine.Object asset) { } } public static AssetImporter GetAtPath(string p) => null; public void SaveAndReimport() { } public void AddRemap(SourceAssetIdentifier identifier, UnityEngine.Object externalObject) { } }
    public enum TextureImporterType { Default = 0, NormalMap = 1 }
    public sealed class TextureImporter : AssetImporter { public TextureImporterType textureType { get; set; } public bool mipmapEnabled { get; set; } public UnityEngine.TextureWrapMode wrapMode { get; set; } public bool isReadable { get; set; } public bool alphaIsTransparency { get; set; } }
    public enum ModelImporterAnimationType { None, Legacy, Generic, Human }
    public sealed class ModelImporterClipAnimation { public string name { get; set; } public bool loopTime { get; set; } public UnityEngine.WrapMode wrapMode { get; set; } }
    public sealed class ModelImporter : AssetImporter { public ModelImporterAnimationType animationType { get; set; } public bool importAnimation { get; set; } public ModelImporterClipAnimation[] clipAnimations { get; set; } public ModelImporterClipAnimation[] defaultClipAnimations => null; public bool ExtractTextures(string folderPath) => false; }
    public static class EditorUtility { public static bool DisplayDialog(string title, string message, string ok) => false; public static void SetDirty(UnityEngine.Object o) { } }
    public static class AssetDatabase { public static string[] FindAssets(string filter, string[] folders) => null; public static string GUIDToAssetPath(string guid) => ""; public static bool IsValidFolder(string p) => false; public static string CreateFolder(string a, string b) => ""; public static void CreateAsset(UnityEngine.Object o, string p) { } public static void SaveAssets() { } public static void Refresh() { } public static bool DeleteAsset(string p) => false; public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object => default; public static UnityEngine.Object[] LoadAllAssetsAtPath(string p) => null; public static void ImportAsset(string p) { } }
}
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path) => false;
        public static bool CloseScene(UnityEngine.SceneManagement.Scene scene, bool remove) => false;
        public static UnityEngine.SceneManagement.Scene OpenScene(string path) => default;
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => false;
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string path => ""; public bool isDirty => false; public UnityEngine.GameObject[] GetRootGameObjects() => null; }
    public static class SceneManager { public static Scene GetActiveScene() => default; public static int sceneCount => 0; }
}
namespace UnityEditor.Build.Reporting
{
    public sealed class BuildReport { }
}
namespace UnityEditor.Build
{
    public interface IOrderedCallback { int callbackOrder { get; } }
    public interface IPreprocessBuildWithReport : IOrderedCallback { void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report); }
    public interface IPostprocessBuildWithReport : IOrderedCallback { void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report); }
    public class BuildFailedException : Exception { public BuildFailedException(string message) : base(message) { } }
}
