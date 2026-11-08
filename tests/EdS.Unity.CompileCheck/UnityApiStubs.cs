// Sous-lot 9.3 : bouchons minimaux de l'API Unity (signatures seulement, aucun comportement), pour compiler en CI les scripts du
// prototype (unity/Assets/EdS) en C# 9 hors d'Unity. Ils détectent les erreurs de C# (noms, types, syntaxe) mais pas un écart avec la
// vraie API d'Unity : seul l'essai dans l'éditeur le fait. Un script qui utilise une API absente d'ici demande d'y ajouter son bouchon.
#pragma warning disable
using System;
namespace UnityEngine
{
    public class Object { public string name { get; set; } public static void Destroy(Object o) { } public static void Destroy(Object o, float t) { } public static T Instantiate<T>(T original) where T : Object => original; public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object => original; public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => original; public static T FindFirstObjectByType<T>() where T : Object => default; public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => null; public static void DestroyImmediate(Object o) { } public static void DontDestroyOnLoad(Object o) { } public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public Transform transform => null; public GameObject gameObject => null; public T GetComponent<T>() => default; public T GetComponentInChildren<T>() => default; public T[] GetComponentsInChildren<T>() => null; }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class Animator : Behaviour { public bool enabled { get; set; } public Avatar avatar { get; set; } public bool applyRootMotion { get; set; } public AnimatorCullingMode cullingMode { get; set; } public RuntimeAnimatorController runtimeAnimatorController { get; set; } }
    public enum AnimatorCullingMode { AlwaysAnimate, CullUpdateTransforms, CullCompletely }
    public class RuntimeAnimatorController : Object { }
    public sealed class Avatar : Object { public bool isValid => false; public bool isHuman => false; }
    public struct BoneWeight { public int boneIndex0 { get; set; } public float weight0 { get; set; } }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => default; public static ScriptableObject CreateInstance(System.Type type) => null; }
    public sealed class GameObject : Object { public GameObject(string n) { } public Transform transform => null; public string tag { get; set; } public T AddComponent<T>() where T : Component => default; public T GetComponent<T>() => default; public T GetComponentInChildren<T>() => default; public T[] GetComponentsInChildren<T>() => null; public T[] GetComponentsInChildren<T>(bool includeInactive) => null; public void SetActive(bool b) { } public bool activeSelf => false; public bool activeInHierarchy => false; public int layer { get; set; } public static GameObject CreatePrimitive(PrimitiveType t) => null; public static GameObject Find(string name) => null; }
    public enum FindObjectsSortMode { None, InstanceID }
    public sealed class ReflectionProbe : Behaviour { public UnityEngine.Rendering.ReflectionProbeMode mode { get; set; } public UnityEngine.Rendering.ReflectionProbeRefreshMode refreshMode { get; set; } public UnityEngine.Rendering.ReflectionProbeTimeSlicingMode timeSlicingMode { get; set; } public int resolution { get; set; } public Vector3 size { get; set; } public bool hdr { get; set; } public int cullingMask { get; set; } public UnityEngine.Rendering.ReflectionProbeClearFlags clearFlags { get; set; } public int RenderProbe() => 0; public Texture realtimeTexture => null; }
    public struct RenderParams { public RenderParams(Material mat) { worldBounds = default; shadowCastingMode = default; receiveShadows = false; } public Bounds worldBounds { get; set; } public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode { get; set; } public bool receiveShadows { get; set; } }
    public static class Graphics { public static void RenderMeshInstanced<T>(in RenderParams rparams, Mesh mesh, int submeshIndex, T[] instanceData, int instanceCount = -1, int startInstance = 0) where T : struct { } }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public class Transform : Component { public Vector3 TransformPoint(Vector3 p) => p; public Vector3 forward => default; public Vector3 right => default; public Vector3 position { get; set; } public Quaternion rotation { get; set; } public Vector3 localPosition { get; set; } public Quaternion localRotation { get; set; } public Vector3 localScale { get; set; } public Vector3 lossyScale => default; public Vector3 eulerAngles { get; set; } public void SetParent(Transform p, bool w) { } public Transform GetChild(int index) => null; public void LookAt(Vector3 v) { } public void SetAsLastSibling() { } public void SetAsFirstSibling() { } public int GetSiblingIndex() => 0; public void SetSiblingIndex(int i) { } }
    public sealed class RectTransform : Transform { public Vector2 anchorMin { get; set; } public Vector2 anchorMax { get; set; } public Vector2 pivot { get; set; } public Vector2 sizeDelta { get; set; } public Vector2 anchoredPosition { get; set; } public Vector2 offsetMin { get; set; } public Vector2 offsetMax { get; set; } }
    public static class RectTransformUtility { public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint) { localPoint = default; return false; } }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public sealed class Canvas : Behaviour { public RenderMode renderMode { get; set; } public int sortingOrder { get; set; } }
    public sealed class CanvasGroup : Behaviour { public float alpha { get; set; } public bool blocksRaycasts { get; set; } public bool interactable { get; set; } }
    public enum SpriteMeshType { FullRect, Tight }
    public sealed class Sprite : Object { public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => null; public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) => null; }
    public struct UIVertex { public Vector3 position; public Color32 color; }
    public sealed class MaterialPropertyBlock { public void SetFloat(string name, float value) { } public void SetFloat(int id, float value) { } public void SetColor(string name, Color value) { } }
    public class Renderer : Component { public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode { get; set; } public bool receiveShadows { get; set; } public void SetPropertyBlock(MaterialPropertyBlock block) { } public Material[] sharedMaterials { get; set; } public Material sharedMaterial { get; set; } public Material material { get; set; } public bool enabled { get; set; } public Bounds bounds => default; public Material[] materials { get; set; } }
    public struct Bounds { public Bounds(Vector3 center, Vector3 size) { } public Vector3 min => default; public Vector3 max => default; public Vector3 size => default; public Vector3 center => default; public Vector3 extents => default; public void Encapsulate(Bounds b) { } }
    public enum WrapMode { Default = 0, Once = 1, Loop = 2, PingPong = 4, ClampForever = 8 }
    public sealed class AnimationState { public string name { get; set; } public WrapMode wrapMode { get; set; } public float length => 0; public float time { get; set; } public float speed { get; set; } }
    public sealed class AnimationClip : Object { public float length => 0; }
    public sealed class Animation : Behaviour, System.Collections.IEnumerable { public System.Collections.IEnumerator GetEnumerator() => null; public void AddClip(AnimationClip clip, string newName) { } public AnimationClip GetClip(string name) => null; public AnimationState this[string name] => null; public void Rewind(string name) { } public void CrossFade(string animation, float fadeLength) { } public bool Play(string animation) => false; public bool Play() => false; public void Stop() { } }
    public enum AudioClipLoadType { DecompressOnLoad = 0, CompressedInMemory = 1, Streaming = 2 }
    public sealed class AudioClip : Object { public float length => 0; public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => null; public bool SetData(float[] data, int offsetSamples) => false; }
    public sealed class AudioSource : Behaviour { public void PlayOneShot(AudioClip clip, float volumeScale) { } public void PlayOneShot(AudioClip clip) { } public AudioClip clip { get; set; } public bool loop { get; set; } public float volume { get; set; } public float pitch { get; set; } public float spatialBlend { get; set; } public bool playOnAwake { get; set; } public bool isPlaying => false; public void Play() { } public void Stop() { } public void Pause() { } public void UnPause() { } public float time { get; set; } public bool ignoreListenerPause { get; set; } public float dopplerLevel { get; set; } public AudioRolloffMode rolloffMode { get; set; } public float minDistance { get; set; } public float maxDistance { get; set; } }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public sealed class AudioListener : Behaviour { }
    public sealed class MeshRenderer : Renderer { }
    public sealed class SkinnedMeshRenderer : Renderer { public bool updateWhenOffscreen { get; set; } public Mesh sharedMesh { get; set; } public Transform[] bones { get; set; } public Transform rootBone { get; set; } public void BakeMesh(Mesh mesh) { } }
    public sealed class MeshFilter : Component { public Mesh mesh { get; set; } public Mesh sharedMesh { get; set; } }
    public sealed class Mesh : Object { public Mesh() { } public Bounds bounds => default; public UnityEngine.Rendering.IndexFormat indexFormat { get; set; } public int vertexCount => 0; public Vector3[] normals { get; set; } public Vector3[] vertices { get; set; } public Vector2[] uv { get; set; } public Vector2[] uv2 { get; set; } public Color[] colors { get; set; } public int[] triangles { get; set; } public BoneWeight[] boneWeights { get; set; } public void Clear() { } public void RecalculateNormals() { } public void RecalculateTangents() { } public void RecalculateBounds() { } }
    public static class PlayerPrefs { public static float GetFloat(string key, float defaultValue) => defaultValue; public static void SetFloat(string key, float value) { } public static int GetInt(string key, int defaultValue) => defaultValue; public static void SetInt(string key, int value) { } public static void Save() { } }
    public class Collider : Component { }
    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public sealed class Light : Behaviour { public LightType type { get; set; } public float intensity { get; set; } public LightShadows shadows { get; set; } public Color color { get; set; } public float range { get; set; } public bool enabled { get; set; } }
    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }
    public static class RenderSettings { public static Material skybox { get; set; } public static UnityEngine.Rendering.AmbientMode ambientMode { get; set; } public static Color ambientSkyColor { get; set; } public static Color ambientEquatorColor { get; set; } public static Color ambientGroundColor { get; set; } public static bool fog { get; set; } public static FogMode fogMode { get; set; } public static Color fogColor { get; set; } public static float fogStartDistance { get; set; } public static float fogEndDistance { get; set; } public static float fogDensity { get; set; } }
    public sealed class TerrainLayer : Object { public TerrainLayer() { } public Texture2D diffuseTexture { get; set; } public Texture2D normalMapTexture { get; set; } public Vector2 tileSize { get; set; } public Vector4 diffuseRemapMin { get; set; } public Vector4 diffuseRemapMax { get; set; } }
    public sealed class TerrainData : Object { public TerrainData() { } public int heightmapResolution { get; set; } public int alphamapResolution { get; set; } public Vector3 size { get; set; } public TerrainLayer[] terrainLayers { get; set; } public void SetAlphamaps(int x, int y, float[,,] map) { } public void SetHeights(int x, int y, float[,] heights) { } }
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
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color32(Color c) => default; public static implicit operator Color(Color32 c) => default; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b) : this(r, g, b, 1) { } public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => default; public static Color black => default; public float maxColorComponent => 0; public static Color Lerp(Color a, Color b, float t) => a; public static Color operator *(Color a, Color b) => a; public static Color operator *(Color a, float b) => a; }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; public static Vector2 one => default; public float magnitude => 0; public Vector2 normalized => default; public static float Distance(Vector2 a, Vector2 b) => 0; public static Vector2 operator +(Vector2 a, Vector2 b) => a; public static Vector2 operator -(Vector2 a, Vector2 b) => a; public static Vector2 operator *(Vector2 a, float f) => a; public static Vector2 operator /(Vector2 a, float f) => a; }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public int fingerId => 0; public Vector2 position => default; public TouchPhase phase => default; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero => default; public static Vector4 operator *(Vector4 a, float f) => a; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero => default; public static Vector3 one => default; public static Vector3 up => default; public static Vector3 forward => default; public float magnitude => 0; public float sqrMagnitude => 0; public Vector3 normalized => this; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a; public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) => a; public static Vector3 Normalize(Vector3 v) => v; public static Vector3 Cross(Vector3 a, Vector3 b) => a; public static float Dot(Vector3 a, Vector3 b) => 0; public static Vector3 operator /(Vector3 a, float d) => a; public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a; public static Vector3 operator *(Vector3 a, float d) => a; public static Vector3 operator *(float d, Vector3 a) => a; }
    public struct Quaternion { public static Quaternion identity => default; public static Quaternion AngleAxis(float angle, Vector3 axis) => default; public static Quaternion Euler(float x, float y, float z) => default; public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a; public static Quaternion LookRotation(Vector3 f) => default; public Vector3 eulerAngles => default; public static Vector3 operator *(Quaternion q, Vector3 v) => v; public static Quaternion operator *(Quaternion a, Quaternion b) => a; }
    public struct Rect { public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } public float x, y, width, height; }
    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) => default; public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => default; }
    public static class Mathf { public static float Clamp(float v, float a, float b) => v; public static int Clamp(int v, int a, int b) => v; public static float Clamp01(float v) => v; public static float Lerp(float a, float b, float t) => a; public static float LerpAngle(float a, float b, float t) => a; public static float Exp(float p) => p; public static float Sin(float f) => f; public static float Cos(float f) => f; public static float Atan2(float y, float x) => y; public static float Abs(float f) => f; public static float Min(float a, float b) => a; public static int Min(int a, int b) => a; public static float Max(float a, float b) => a; public static int Max(int a, int b) => a; public static float MoveTowards(float current, float target, float maxDelta) => current; public static float MoveTowardsAngle(float current, float target, float maxDelta) => current; public static float PerlinNoise(float x, float y) => 0f; public static float Repeat(float t, float length) => t; public static int FloorToInt(float f) => 0; public static int CeilToInt(float f) => 0; public static float Round(float f) => f; public static float Floor(float f) => f; public static float Ceil(float f) => f; public static int RoundToInt(float f) => 0; public static float Pow(float f, float p) => f; public static float Sqrt(float f) => f; public static bool Approximately(float a, float b) => a == b; public const float PI = 3.14159274f; public const float Deg2Rad = 0.0174532924f; public const float Rad2Deg = 57.29578f; }
    public enum KeyCode { None, Backspace, Tab, Escape, Space, Return, KeypadEnter, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9, UpArrow, DownArrow, RightArrow, LeftArrow, F1, F2, F3, F4, F5, F6, F8, A, B, C, D, E, F, G, H, I, J, K, L, M, N, P, Q, R, S, T, V, W, X, Z, Semicolon, LeftShift, RightShift, LeftAlt, RightAlt }
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor { public static CursorLockMode lockState { get; set; } public static bool visible { get; set; } }
    public static class Input { public static bool anyKeyDown => false; public static bool GetKey(KeyCode k) => false; public static bool GetKeyDown(KeyCode k) => false; public static bool GetMouseButton(int b) => false; public static bool GetMouseButtonDown(int b) => false; public static bool GetMouseButtonUp(int b) => false; public static Vector3 mousePosition => default; public static float GetAxis(string a) => 0; public static Vector2 mouseScrollDelta => default; public static bool touchSupported => false; public static int touchCount => 0; public static Touch GetTouch(int i) => default; }
    public static class Time { public static float time => 0; public static float deltaTime => 0; public static float unscaledDeltaTime => 0; public static float unscaledTime => 0; }
    // Essai réaliste 7 : effets en particules (Effects).
    public struct Keyframe { public Keyframe(float time, float value) { } }
    public sealed class AnimationCurve { public AnimationCurve(params Keyframe[] keys) { } public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd) => null; }
    public struct GradientColorKey { public GradientColorKey(Color col, float time) { } }
    public struct GradientAlphaKey { public GradientAlphaKey(float alpha, float time) { } }
    public sealed class Gradient { public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys) { } }
    public enum ParticleSystemRenderMode { Billboard = 0, Stretch = 1, HorizontalBillboard = 2, VerticalBillboard = 3, Mesh = 4, None = 5 }
    public enum ParticleSystemCullingMode { Automatic = 0, PauseAndCatchup = 1, Pause = 2, AlwaysSimulate = 3 }
    public enum ParticleSystemSimulationSpace { Local = 0, World = 1, Custom = 2 }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear = 0, StopEmitting = 1 }
    public sealed class ParticleSystemRenderer : Renderer { public ParticleSystemRenderMode renderMode { get; set; } public float velocityScale { get; set; } public float lengthScale { get; set; } public Material trailMaterial { get; set; } }
    public enum ParticleSystemScalingMode { Hierarchy, Local, Shape }
    public enum ParticleSystemShapeType { Sphere = 0, Hemisphere = 2, Cone = 4, Box = 5 }
    public sealed class ParticleSystem : Component
    {
        public struct MinMaxCurve { public MinMaxCurve(float constant) { } public MinMaxCurve(float min, float max) { } public MinMaxCurve(float multiplier, AnimationCurve curve) { } public static implicit operator MinMaxCurve(float constant) => default; }
        public struct MinMaxGradient { public MinMaxGradient(Gradient gradient) { } public MinMaxGradient(Color color) { } public MinMaxGradient(Color min, Color max) { } public static implicit operator MinMaxGradient(Color color) => default; }
        public struct MainModule { public bool loop { get; set; } public bool playOnAwake { get; set; } public float duration { get; set; } public ParticleSystemSimulationSpace simulationSpace { get; set; } public int maxParticles { get; set; } public MinMaxCurve gravityModifier { get; set; } public MinMaxCurve startSpeed { get; set; } public ParticleSystemCullingMode cullingMode { get; set; } public ParticleSystemScalingMode scalingMode { get; set; } public MinMaxCurve startLifetime { get; set; } public MinMaxCurve startSize { get; set; } public MinMaxCurve startRotation { get; set; } public MinMaxGradient startColor { get; set; } }
        public struct EmissionModule { public bool enabled { get; set; } public MinMaxCurve rateOverTime { get; set; } }
        public struct ShapeModule { public bool enabled { get; set; } public ParticleSystemShapeType shapeType { get; set; } public float angle { get; set; } public float radius { get; set; } public Vector3 scale { get; set; } }
        public struct ColorOverLifetimeModule { public bool enabled { get; set; } public MinMaxGradient color { get; set; } }
        public struct SizeOverLifetimeModule { public bool enabled { get; set; } public MinMaxCurve size { get; set; } }
        public struct EmitParams { public Vector3 position { get; set; } public Vector3 velocity { get; set; } public float startSize { get; set; } public float startLifetime { get; set; } public Color32 startColor { get; set; } public float rotation { get; set; } public bool applyShapeToPosition { get; set; } }
        public MainModule main => default;
        public EmissionModule emission => default;
        public ShapeModule shape => default;
        public ColorOverLifetimeModule colorOverLifetime => default;
        public SizeOverLifetimeModule sizeOverLifetime => default;
        public void Emit(EmitParams emitParams, int count) { }
        public void Play() { }
        public void Stop(bool withChildren, ParticleSystemStopBehavior stopBehavior) { }
    }
    public enum RuntimePlatform { WindowsEditor, WindowsPlayer, Android, IPhonePlayer }
    public static class Application { public static string unityVersion => ""; public static bool isEditor => false; public static bool isFocused => false; public static string dataPath => ""; public static string streamingAssetsPath => ""; public static string persistentDataPath => ""; public static bool isMobilePlatform => false; public static RuntimePlatform platform => default; public static int targetFrameRate { get; set; } }
    public class AsyncOperation { }
    public static class SleepTimeout { public const int NeverSleep = -1; }
    public enum ScreenOrientation { Portrait = 1, LandscapeLeft = 3, AutoRotation = 5 }
    public enum ShadowQuality { Disable, HardOnly, All }
    public static class Debug { public static void Log(object m) { } public static void LogWarning(object m) { } public static void LogException(Exception e) { } }
    public sealed class Shader : Object { public static Shader Find(string n) => null; public static int PropertyToID(string n) => 0; public bool isSupported => false; public int subshaderCount => 0; public UnityEngine.Rendering.ShaderTagId FindSubshaderTagValue(int subshaderIndex, UnityEngine.Rendering.ShaderTagId tagName) => default; }
    public static class Resources { public static T Load<T>(string path) where T : Object => default; public static T GetBuiltinResource<T>(string path) where T : Object => default; }
    public class Material : Object { public Material(Shader s) { } public Material(Material source) { } public void CopyPropertiesFromMaterial(Material m) { } public Shader shader { get; set; } public bool enableInstancing { get; set; } public float GetFloat(string n) => 0f; public bool HasProperty(string n) => false; public void SetColor(string n, Color c) { } public Color GetColor(string n) => default; public void SetTexture(string n, Texture t) { } public Texture GetTexture(string n) => null; public void SetTextureOffset(string n, Vector2 v) { } public void SetTextureScale(string n, Vector2 v) { } public void SetFloat(string n, float f) { } public void SetFloat(int id, float f) { } public void EnableKeyword(string k) { } public int renderQueue { get; set; } public Vector2 GetTextureScale(string n) => default; public Vector2 GetTextureOffset(string n) => default; public string[] shaderKeywords { get; set; } public void SetOverrideTag(string tag, string value) { } public void SetVector(string n, Vector4 v) { } }
    public class Texture : Object { public int width => 0; public int height => 0; public TextureWrapMode wrapMode { get; set; } }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public sealed class Texture2D : Texture { public Texture2D(int width, int height, TextureFormat format, bool mipChain) { } public Texture2D(int width, int height, TextureFormat format, bool mipChain, bool linear) { } public void Apply(bool updateMipmaps) { } public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { } public static Texture2D whiteTexture => null; public FilterMode filterMode { get; set; } public void LoadRawTextureData(byte[] data) { } public void Apply() { } public Color32[] GetPixels32() => null; public void SetPixels32(Color32[] colors) { } public byte[] EncodeToPNG() => null; }
    public enum TextureFormat { RGBA32 = 4 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public sealed class Font : Object { }
    public sealed class RectOffset { public RectOffset() { } public RectOffset(int left, int right, int top, int bottom) { } }
    public sealed class GUIContent { public static GUIContent none => null; }
    public enum FontStyle { Normal, Bold }
    public sealed class GUIStyleState { public Color textColor { get; set; } public Texture2D background { get; set; } }
    public sealed class GUIStyle { public GUIStyle(GUIStyle other) { } public int fontSize { get; set; } public bool richText { get; set; } public TextAnchor alignment { get; set; } public FontStyle fontStyle { get; set; } public GUIStyleState normal { get; set; } public GUIStyleState hover { get; set; } public GUIStyleState active { get; set; } public GUIStyleState focused { get; set; } public RectOffset border { get; set; } public Font font { get; set; } }
    public sealed class GUISkin : Object { public GUIStyle box => null; public GUIStyle label => null; public GUIStyle button => null; public GUIStyle textField => null; }
    public static class GUI { public static GUISkin skin => null; public static Color color { get; set; } public static Matrix4x4 matrix { get; set; } public static void DrawTexture(Rect r, Texture t) { } public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect coords) { } public static void Box(Rect r, GUIContent c, GUIStyle s) { } public static void Label(Rect r, string t, GUIStyle s) { } public static bool Button(Rect r, string t, GUIStyle s) => false; public static string TextField(Rect r, string t, int maxLength, GUIStyle s) => t; }
    public static class GUILayout { public static void BeginHorizontal() { } public static void EndHorizontal() { } public static bool Button(string text) => false; public static void Label(string text, GUIStyle style) { } public static void Space(float pixels) { } }
    public static class GUIUtility { public static int keyboardControl { get; set; } }
    public static class Screen { public static int width => 0; public static int height => 0; public static int sleepTimeout { get; set; } public static bool autorotateToPortrait { get; set; } public static bool autorotateToPortraitUpsideDown { get; set; } public static bool autorotateToLandscapeLeft { get; set; } public static bool autorotateToLandscapeRight { get; set; } public static ScreenOrientation orientation { get; set; } }
    public static class QualitySettings { public static UnityEngine.Rendering.RenderPipelineAsset renderPipeline { get; set; } public static ShadowQuality shadows { get; set; } public static int antiAliasing { get; set; } }
}
namespace UnityEngine.Playables
{
    // Sous-lot V2.9.2 : graphe d'animation des corps humains (dans Unity, SetInputWeight, SetTime, SetSpeed, Play, Pause, GetPlayState et
    // SetSourcePlayable sont des méthodes d'extension : même écriture à l'appel).
    public interface IPlayable { }
    public enum DirectorUpdateMode { DSPClock, GameTime, UnscaledGameTime, Manual }
    public enum PlayState { Paused, Playing }
    public struct PlayableGraph { public static PlayableGraph Create(string name) => default; public void SetTimeUpdateMode(DirectorUpdateMode mode) { } public void Play() { } public void Destroy() { } public bool IsValid() => false; public bool Connect<U, V>(U source, int sourceOutputPort, V destination, int destinationInputPort) where U : struct, IPlayable where V : struct, IPlayable => false; }
}
namespace UnityEngine.Animations
{
    public struct AnimationMixerPlayable : UnityEngine.Playables.IPlayable { public static AnimationMixerPlayable Create(UnityEngine.Playables.PlayableGraph graph, int inputCount) => default; public void SetInputWeight(int inputIndex, float weight) { } }
    public struct AnimationClipPlayable : UnityEngine.Playables.IPlayable { public static AnimationClipPlayable Create(UnityEngine.Playables.PlayableGraph graph, UnityEngine.AnimationClip clip) => default; public void SetApplyFootIK(bool value) { } public void SetTime(double time) { } public void SetSpeed(double speed) { } public void Play() { } public void Pause() { } public UnityEngine.Playables.PlayState GetPlayState() => default; }
    public struct AnimationPlayableOutput { public static AnimationPlayableOutput Create(UnityEngine.Playables.PlayableGraph graph, string name, UnityEngine.Animator target) => default; public void SetSourcePlayable<V>(V value) where V : struct, UnityEngine.Playables.IPlayable { } }
}
namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : MonoBehaviour { public virtual bool IsActive() => false; }
    public class EventSystem : UIBehaviour { public static EventSystem current => null; public bool IsPointerOverGameObject() => false; }
    public abstract class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class PointerEventData { public enum InputButton { Left = 0, Right = 1, Middle = 2 } public InputButton button { get; set; } public int pointerId { get; set; } public Vector2 position { get; set; } public bool dragging { get; set; } }
    public interface IEventSystemHandler { }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    // Sous-lot V1.5 : glisser une compétence sur une place (souris et doigt).
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IDropHandler : IEventSystemHandler { void OnDrop(PointerEventData eventData); }
}
namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    public class Graphic : UIBehaviour { public virtual Color color { get; set; } public bool raycastTarget { get; set; } public RectTransform rectTransform => null; public virtual Material material { get; set; } }
    public class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic { public enum Type { Simple, Sliced, Tiled, Filled } public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 } public enum OriginHorizontal { Left, Right } public enum OriginVertical { Bottom, Top } public enum Origin360 { Bottom, Right, Top, Left } public Sprite sprite { get; set; } public Type type { get; set; } public FillMethod fillMethod { get; set; } public int fillOrigin { get; set; } public float fillAmount { get; set; } public bool fillClockwise { get; set; } public bool fillCenter { get; set; } }
    public class RawImage : MaskableGraphic { public Texture texture { get; set; } public Rect uvRect { get; set; } }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public class Text : MaskableGraphic { public Font font { get; set; } public int fontSize { get; set; } public TextAnchor alignment { get; set; } public HorizontalWrapMode horizontalOverflow { get; set; } public VerticalWrapMode verticalOverflow { get; set; } public bool supportRichText { get; set; } public virtual string text { get; set; } }
    public class Mask : UIBehaviour { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : UIBehaviour { }
    public class GraphicRaycaster : UIBehaviour { }
    public class CanvasScaler : UIBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink } public ScaleMode uiScaleMode { get; set; } public Vector2 referenceResolution { get; set; } public ScreenMatchMode screenMatchMode { get; set; } public float matchWidthOrHeight { get; set; } }
    public class VertexHelper { public int currentVertCount => 0; public void PopulateUIVertex(ref UIVertex vertex, int i) { } public void SetUIVertex(UIVertex vertex, int i) { } }
    public abstract class BaseMeshEffect : UIBehaviour { public abstract void ModifyMesh(VertexHelper vh); }
    public class Shadow : BaseMeshEffect { public Color effectColor { get; set; } public Vector2 effectDistance { get; set; } public override void ModifyMesh(VertexHelper vh) { } }
    public class Outline : Shadow { }
}
namespace TMPro
{
    public enum TextAlignmentOptions { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }
    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }
    [Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32 }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public struct VertexGradient { public VertexGradient(UnityEngine.Color topLeft, UnityEngine.Color topRight, UnityEngine.Color bottomLeft, UnityEngine.Color bottomRight) { } }
    public class TMP_Asset : UnityEngine.ScriptableObject { }
    public class TMP_FontAsset : TMP_Asset { public static TMP_FontAsset CreateFontAsset(UnityEngine.Font font) => null; public UnityEngine.Material material { get; set; } }
    public class TMP_Settings : UnityEngine.ScriptableObject { }
    public abstract class TMP_Text : UnityEngine.UI.MaskableGraphic { public virtual string text { get; set; } public TMP_FontAsset font { get; set; } public float fontSize { get; set; } public TextAlignmentOptions alignment { get; set; } public float characterSpacing { get; set; } public TextOverflowModes overflowMode { get; set; } public FontStyles fontStyle { get; set; } public bool richText { get; set; } public TextWrappingModes textWrappingMode { get; set; } public bool enableVertexGradient { get; set; } public VertexGradient colorGradient { get; set; } public UnityEngine.Color32 outlineColor { get; set; } public float outlineWidth { get; set; } public virtual UnityEngine.Material fontMaterial { get; set; } public virtual UnityEngine.Material fontSharedMaterial { get; set; } public virtual void UpdateMeshPadding() { } }
    public class TextMeshProUGUI : TMP_Text { }
}
namespace UnityEngine.Networking
{
    public sealed class DownloadHandler { public byte[] data => null; public string text => null; }
    public sealed class UnityWebRequestAsyncOperation : UnityEngine.AsyncOperation { }
    public sealed class UnityWebRequest : IDisposable { public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError } public static UnityWebRequest Get(string uri) => null; public UnityWebRequestAsyncOperation SendWebRequest() => null; public Result result => default; public string error => null; public DownloadHandler downloadHandler => null; public void Dispose() { } }
}
namespace UnityEngine.Rendering
{
    public struct ShaderTagId { public ShaderTagId(string name) { } public string name => ""; }
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public class VolumeComponent : ScriptableObject { public bool active; }
    public class VolumeParameter<T> { public T value { get; set; } public bool overrideState { get; set; } public void Override(T x) { } }
    public class FloatParameter : VolumeParameter<float> { }
    public sealed class ClampedFloatParameter : FloatParameter { }
    public sealed class MinFloatParameter : FloatParameter { }
    public class ColorParameter : VolumeParameter<UnityEngine.Color> { }
    public sealed class VolumeProfile : ScriptableObject { public T Add<T>(bool overrides = false) where T : VolumeComponent => default; }
    public sealed class Volume : MonoBehaviour { public bool isGlobal { get; set; } public float priority { get; set; } public VolumeProfile sharedProfile { get; set; } }
    public abstract class RenderPipelineAsset : ScriptableObject { }
    public enum ReflectionProbeMode { Baked, Realtime, Custom }
    public enum ReflectionProbeRefreshMode { OnAwake, EveryFrame, ViaScripting }
    public enum ReflectionProbeTimeSlicingMode { AllFacesAtOnce, IndividualFaces, NoTimeSlicing }
    public enum ReflectionProbeClearFlags { Skybox = 1, SolidColor = 2 }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline => null; public static RenderPipelineAsset defaultRenderPipeline { get; set; } }
}
namespace UnityEngine.Rendering.Universal
{
    // Correctif du 27 octobre 2026 (essai de Damien) : ce nom existe aussi dans UnityEngine ; avec les deux espaces de noms ouverts, il faut le
    // qualifier, sinon Unity refuse de compiler (CS0104). Déclaré ici pour que la CI voie la même ambiguïté.
    public enum ShadowQuality { Disabled, HardShadows, SoftShadows }
    public abstract class ScriptableRendererFeature : ScriptableObject { }
    public abstract class ScriptableRendererData : ScriptableObject { public System.Collections.Generic.List<ScriptableRendererFeature> rendererFeatures => null; }
    public sealed class UniversalRendererData : ScriptableRendererData { public PostProcessData postProcessData; }
    public sealed class PostProcessData : ScriptableObject { }
    public enum TonemappingMode { None, Neutral, ACES }
    public sealed class TonemappingModeParameter : UnityEngine.Rendering.VolumeParameter<TonemappingMode> { }
    public sealed class Tonemapping : UnityEngine.Rendering.VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class ColorAdjustments : UnityEngine.Rendering.VolumeComponent { public UnityEngine.Rendering.FloatParameter postExposure; public UnityEngine.Rendering.ClampedFloatParameter contrast; public UnityEngine.Rendering.ClampedFloatParameter saturation; public UnityEngine.Rendering.ColorParameter colorFilter; }
    public sealed class Bloom : UnityEngine.Rendering.VolumeComponent { public UnityEngine.Rendering.MinFloatParameter threshold; public UnityEngine.Rendering.MinFloatParameter intensity; }
    public sealed class Vignette : UnityEngine.Rendering.VolumeComponent { public UnityEngine.Rendering.ClampedFloatParameter intensity; }
    public sealed class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing { get; set; } }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }
    public sealed class UniversalRenderPipelineAsset : RenderPipelineAsset { public static UniversalRenderPipelineAsset Create(ScriptableRendererData d = null) => null; public bool supportsCameraOpaqueTexture { get; set; } }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class InitializeOnLoadMethodAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem : Attribute { public MenuItem(string p) { } }
    // Sous-lot V2.9.3 : fenêtre du vérificateur d'essai.
    public class EditorWindow : UnityEngine.ScriptableObject { public static T GetWindow<T>(string title) where T : EditorWindow => default; public UnityEngine.Vector2 minSize { get; set; } public void Show() { } public void Repaint() { } }
    public static class EditorGUILayout { public static UnityEngine.Vector2 BeginScrollView(UnityEngine.Vector2 scroll) => scroll; public static void EndScrollView() { } }
    public static class EditorStyles { public static UnityEngine.GUIStyle boldLabel => null; public static UnityEngine.GUIStyle miniLabel => null; public static UnityEngine.GUIStyle wordWrappedLabel => null; public static UnityEngine.GUIStyle wordWrappedMiniLabel => null; }
    public static class SessionState { public static bool GetBool(string key, bool defaultValue) => defaultValue; public static void SetBool(string key, bool value) { } }
    public static class EditorApplication { public delegate void CallbackFunction(); public static CallbackFunction delayCall; public static bool isPlayingOrWillChangePlaymode => false; public static bool isPlaying { get; set; } }
    public sealed class SceneAsset : UnityEngine.Object { }
    public static class BuildPipeline { public static bool isBuildingPlayer => false; }
    public sealed class EditorBuildSettingsScene { public EditorBuildSettingsScene(string path, bool enabled) { } public string path => ""; public bool enabled => false; }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } }
    public class AssetImporter : UnityEngine.Object { public struct SourceAssetIdentifier { public SourceAssetIdentifier(UnityEngine.Object asset) { } } public static AssetImporter GetAtPath(string p) => null; public void SaveAndReimport() { } public void AddRemap(SourceAssetIdentifier identifier, UnityEngine.Object externalObject) { } public bool RemoveRemap(SourceAssetIdentifier identifier) => false; public System.Collections.Generic.Dictionary<SourceAssetIdentifier, UnityEngine.Object> GetExternalObjectMap() => null; }
    [System.Flags] public enum ImportAssetOptions { Default = 0, ForceUpdate = 1 }
    public enum TextureImporterType { Default = 0, NormalMap = 1 }
    public enum TextureImporterNPOTScale { None = 0, ToNearest = 1, ToLarger = 2, ToSmaller = 3 }
    public enum TextureImporterCompression { Uncompressed = 0, Compressed = 1, CompressedHQ = 2, CompressedLQ = 3 }
    public class AssetPostprocessor { public string assetPath => ""; public AssetImporter assetImporter => null; public virtual uint GetVersion() => 0; }
    public sealed class TextureImporter : AssetImporter { public TextureImporterNPOTScale npotScale { get; set; } public TextureImporterCompression textureCompression { get; set; } public UnityEngine.FilterMode filterMode { get; set; } public TextureImporterType textureType { get; set; } public bool mipmapEnabled { get; set; } public bool sRGBTexture { get; set; } public UnityEngine.TextureWrapMode wrapMode { get; set; } public bool isReadable { get; set; } public bool alphaIsTransparency { get; set; } public bool mipMapsPreserveCoverage { get; set; } public float alphaTestReferenceValue { get; set; } public int maxTextureSize { get; set; } public TextureImporterPlatformSettings GetPlatformTextureSettings(string platform) => null; public void SetPlatformTextureSettings(TextureImporterPlatformSettings settings) { } }
    public sealed class TextureImporterPlatformSettings { public bool overridden { get; set; } public int maxTextureSize { get; set; } }
    public sealed class SerializedProperty { public float floatValue { get; set; } public int intValue { get; set; } public bool boolValue { get; set; } public long longValue { get; set; } public UnityEngine.Object objectReferenceValue { get; set; } public int arraySize { get; set; } public SerializedProperty GetArrayElementAtIndex(int i) => null; }
    public sealed class SerializedObject { public SerializedObject(UnityEngine.Object o) { } public SerializedProperty FindProperty(string name) => null; public bool ApplyModifiedPropertiesWithoutUndo() => false; }
    public enum ModelImporterAnimationType { None, Legacy, Generic, Human }
    public sealed class ModelImporterClipAnimation { public string name { get; set; } public string takeName { get; set; } public bool loopTime { get; set; } public bool loopPose { get; set; } public UnityEngine.WrapMode wrapMode { get; set; } public bool lockRootRotation { get; set; } public bool lockRootHeightY { get; set; } public bool lockRootPositionXZ { get; set; } public bool keepOriginalOrientation { get; set; } public bool keepOriginalPositionY { get; set; } public bool keepOriginalPositionXZ { get; set; } }
    public enum ModelImporterAvatarSetup { NoAvatar, CreateFromThisModel, CopyFromOther }
    public sealed class ModelImporter : AssetImporter { public ModelImporterAnimationType animationType { get; set; } public ModelImporterAvatarSetup avatarSetup { get; set; } public bool importAnimation { get; set; } public ModelImporterClipAnimation[] clipAnimations { get; set; } public ModelImporterClipAnimation[] defaultClipAnimations => null; public bool ExtractTextures(string folderPath) => false; }
    public enum AudioCompressionFormat { PCM = 0, Vorbis = 1, ADPCM = 2 }
    public struct AudioImporterSampleSettings { public UnityEngine.AudioClipLoadType loadType; public AudioCompressionFormat compressionFormat; public float quality; }
    public sealed class AudioImporter : AssetImporter { public AudioImporterSampleSettings defaultSampleSettings { get; set; } public bool forceToMono { get; set; } public bool ContainsSampleSettingsOverride(string platform) => false; public bool ClearSampleSettingOverride(string platform) => false; }
    public static class PrefabUtility { public static UnityEngine.GameObject SaveAsPrefabAsset(UnityEngine.GameObject instance, string path) => null; public static T GetCorrespondingObjectFromSource<T>(T componentOrGameObject) where T : UnityEngine.Object => null; }
    public static class EditorUtility { public static bool scriptCompilationFailed => false; public static bool DisplayDialog(string title, string message, string ok) => false; public static bool DisplayDialog(string title, string message, string ok, string cancel) => false; public static void OpenWithDefaultApp(string path) { } public static void SetDirty(UnityEngine.Object o) { } public static void DisplayProgressBar(string title, string info, float progress) { } public static void ClearProgressBar() { } }
    public static class AssetDatabase { public static string GetAssetPath(UnityEngine.Object o) => ""; public static void AddObjectToAsset(UnityEngine.Object o, UnityEngine.Object asset) { } public static bool TryGetGUIDAndLocalFileIdentifier(UnityEngine.Object o, out string guid, out long localId) { guid = ""; localId = 0; return false; } public static string[] FindAssets(string filter, string[] folders) => null; public static string GUIDToAssetPath(string guid) => ""; public static bool IsValidFolder(string p) => false; public static string CreateFolder(string a, string b) => ""; public static void CreateAsset(UnityEngine.Object o, string p) { } public static void SaveAssets() { } public static void Refresh() { } public static bool DeleteAsset(string p) => false; public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object => default; public static UnityEngine.Object[] LoadAllAssetsAtPath(string p) => null; public static void ImportAsset(string p) { } public static void ImportAsset(string p, ImportAssetOptions options) { } public static void ImportPackage(string packagePath, bool interactive) { } public static void StartAssetEditing() { } public static void StopAssetEditing() { } }
}
namespace UnityEditor.PackageManager
{
    public sealed class PackageInfo { public static PackageInfo FindForAssetPath(string assetPath) => null; public string resolvedPath => ""; public string name => ""; }
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
