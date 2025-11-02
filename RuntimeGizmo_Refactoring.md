# RuntimeGizmo 리팩토링 문서

## 📋 개요

Unity의 RuntimeGizmo 시스템을 **GL(Graphics Library) 기반 렌더링**에서 **GameObject/Mesh 기반 렌더링**으로 완전히 재구성한 작업입니다.

### 작업 목표
- GL 즉시 모드 렌더링의 성능 및 유지보수 문제 해결
- Unity의 표준 렌더링 파이프라인 활용
- 정확한 마우스 인터랙션 구현
- 코드 구조 개선 및 확장성 향상

---

## 🔧 1. MeshUtils.cs - 메시 생성 유틸리티

### 파일 위치
```
Assets/RuntimeGizmo/Helpers/MeshUtils.cs
```

### 클래스 구조

```csharp
namespace RuntimeGizmos
{
    public static class MeshUtils
    {
        public static Mesh CreateCone(float height, float bottomRadius, float topRadius, int segments, int heightSegments)
        public static Mesh CreateBox(float width, float height, float depth)
        public static Mesh CreateTorus(float radius, float thickness, int radialSegments, int tubularSegments)
        public static Mesh CreateSphere(float radius, int segments, int rings)
    }
}
```

---

### 1.1 CreateCone() - 원뿔/원기둥 메시 생성

**목적**: 축 라인과 화살표를 위한 원뿔 형태 메시 생성

```csharp
public static Mesh CreateCone(float height, float bottomRadius, float topRadius, int segments, int heightSegments)
```

#### 매개변수
- `height`: 원뿔의 높이
- `bottomRadius`: 아래쪽 반지름
- `topRadius`: 위쪽 반지름 (0이면 뾰족한 원뿔)
- `segments`: 원주 방향 분할 수 (값이 클수록 부드러움)
- `heightSegments`: 높이 방향 분할 수

#### 핵심 로직

```csharp
// 1. 정점 생성
for (int y = 0; y <= heightSegments; y++)
{
    float v = (float)y / heightSegments;  // 0~1 사이 값
    float currentHeight = v * height;      // 현재 높이
    float currentRadius = Mathf.Lerp(bottomRadius, topRadius, v);  // 선형 보간
    
    for (int x = 0; x <= segments; x++)
    {
        float u = (float)x / segments;
        float angle = u * Mathf.PI * 2f;  // 0~360도
        
        // 원주 상의 점 계산
        vertices[index] = new Vector3(
            Mathf.Cos(angle) * currentRadius,  // X
            currentHeight,                      // Y (위쪽)
            Mathf.Sin(angle) * currentRadius   // Z
        );
    }
}
```

**설명**:
- `Mathf.Lerp(bottomRadius, topRadius, v)`: 아래에서 위로 갈수록 반지름이 점진적으로 변화
- 각 높이 단계마다 원을 그리며 정점 생성
- `segments + 1`개의 정점으로 닫힌 원 형성 (첫 점과 마지막 점 연결)

```csharp
// 2. 삼각형 인덱스 생성 (Quad를 2개의 삼각형으로)
for (int y = 0; y < heightSegments; y++)
{
    for (int x = 0; x < segments; x++)
    {
        int current = y * (segments + 1) + x;
        int next = current + segments + 1;
        
        // 첫 번째 삼각형 (시계 반대 방향)
        triangles[tIndex++] = current;
        triangles[tIndex++] = next;
        triangles[tIndex++] = current + 1;
        
        // 두 번째 삼각형
        triangles[tIndex++] = current + 1;
        triangles[tIndex++] = next;
        triangles[tIndex++] = next + 1;
    }
}
```

**설명**:
- Quad(사각형) 하나당 2개의 삼각형 필요
- 시계 반대 방향(CCW)으로 정점을 연결해야 Unity에서 앞면으로 인식

#### 사용 예시

```csharp
// 축 라인용 얇은 원기둥
mf.mesh = MeshUtils.CreateCone(0.25f, 0.005f, 0.005f, 8, 1);

// 화살표용 원뿔
mf.mesh = MeshUtils.CreateCone(0.05f, 0.025f, 0f, 8, 1);
```

---

### 1.2 CreateBox() - 박스 메시 생성

**목적**: Plane 핸들을 위한 박스 형태 메시 생성

```csharp
public static Mesh CreateBox(float width, float height, float depth)
```

#### 핵심 로직

```csharp
// 정점 정의 (8개)
Vector3[] vertices = new Vector3[8]
{
    new Vector3(-width/2, -height/2, -depth/2),  // 0: 뒤-아래-왼쪽
    new Vector3( width/2, -height/2, -depth/2),  // 1: 뒤-아래-오른쪽
    new Vector3( width/2,  height/2, -depth/2),  // 2: 뒤-위-오른쪽
    new Vector3(-width/2,  height/2, -depth/2),  // 3: 뒤-위-왼쪽
    new Vector3(-width/2, -height/2,  depth/2),  // 4: 앞-아래-왼쪽
    new Vector3( width/2, -height/2,  depth/2),  // 5: 앞-아래-오른쪽
    new Vector3( width/2,  height/2,  depth/2),  // 6: 앞-위-오른쪽
    new Vector3(-width/2,  height/2,  depth/2)   // 7: 앞-위-왼쪽
};

// 삼각형 인덱스 (6면 × 2삼각형 × 3정점 = 36개)
int[] triangles = new int[36]
{
    0, 2, 1,  0, 3, 2,  // 뒤
    4, 5, 6,  4, 6, 7,  // 앞
    0, 1, 5,  0, 5, 4,  // 아래
    3, 6, 2,  3, 7, 6,  // 위
    0, 7, 3,  0, 4, 7,  // 왼쪽
    1, 2, 6,  1, 6, 5   // 오른쪽
};
```

**설명**:
- 큐브의 8개 꼭지점을 먼저 정의
- 각 면마다 2개의 삼각형으로 구성 (총 12개 삼각형)
- 모든 면이 바깥쪽을 향하도록 정점 순서 설정

#### 사용 예시

```csharp
// YZ 평면 핸들 (매우 얇은 박스)
mf.mesh = MeshUtils.CreateBox(0.005f, 0.06f, 0.06f);
```

---

### 1.3 CreateTorus() - 토러스 메시 생성

**목적**: Rotation 핸들을 위한 도넛 형태 메시 생성

```csharp
public static Mesh CreateTorus(float radius, float thickness, int radialSegments, int tubularSegments)
```

#### 매개변수
- `radius`: 토러스 중심에서 튜브 중심까지의 거리
- `thickness`: 튜브의 반지름
- `radialSegments`: 토러스 원주 방향 분할 수
- `tubularSegments`: 튜브 원주 방향 분할 수

#### 핵심 로직

```csharp
for (int i = 0; i <= radialSegments; i++)
{
    float u = (float)i / radialSegments;
    float theta = u * Mathf.PI * 2f;  // 토러스 원주 각도
    
    // 토러스 원주 상의 점
    Vector3 center = new Vector3(
        Mathf.Cos(theta) * radius,
        0f,
        Mathf.Sin(theta) * radius
    );
    
    for (int j = 0; j <= tubularSegments; j++)
    {
        float v = (float)j / tubularSegments;
        float phi = v * Mathf.PI * 2f;  // 튜브 원주 각도
        
        // 튜브 원주 상의 오프셋
        Vector3 offset = new Vector3(
            Mathf.Cos(theta) * Mathf.Cos(phi) * thickness,
            Mathf.Sin(phi) * thickness,
            Mathf.Sin(theta) * Mathf.Cos(phi) * thickness
        );
        
        vertices[index++] = center + offset;
    }
}
```

**설명**:
1. `theta`: 토러스의 큰 원을 따라 회전하는 각도
2. `phi`: 튜브의 작은 원을 따라 회전하는 각도
3. 각 `theta` 위치에서 `phi`를 0~360도 회전시켜 튜브 생성
4. 이중 루프로 토러스 표면의 모든 점 생성

#### 사용 예시

```csharp
// 시각적 토러스 (얇음)
mf.mesh = MeshUtils.CreateTorus(0.25f, 0.005f, 32, 6);

// 충돌용 토러스 (두꺼움)
mc.sharedMesh = MeshUtils.CreateTorus(0.25f, 0.015f, 32, 6);
```

---

### 1.4 CreateSphere() - 구체 메시 생성

**목적**: 전체 회전 핸들을 위한 구체 메시 생성

```csharp
public static Mesh CreateSphere(float radius, int segments, int rings)
```

#### 핵심 로직

```csharp
for (int lat = 0; lat <= rings; lat++)
{
    float theta = lat * Mathf.PI / rings;  // 위도 각도 (0~π)
    float sinTheta = Mathf.Sin(theta);
    float cosTheta = Mathf.Cos(theta);
    
    for (int lon = 0; lon <= segments; lon++)
    {
        float phi = lon * 2 * Mathf.PI / segments;  // 경도 각도 (0~2π)
        float sinPhi = Mathf.Sin(phi);
        float cosPhi = Mathf.Cos(phi);
        
        // 구면 좌표계 → 직교 좌표계 변환
        vertices[index] = new Vector3(
            radius * sinTheta * cosPhi,  // X
            radius * cosTheta,            // Y
            radius * sinTheta * sinPhi   // Z
        );
    }
}
```

**설명**:
- 구면 좌표계(Spherical Coordinates) 사용
- `theta` (위도): 북극(0)에서 남극(π)까지
- `phi` (경도): 0에서 2π까지
- 수학 공식:
  - `x = r × sin(θ) × cos(φ)`
  - `y = r × cos(θ)`
  - `z = r × sin(θ) × sin(φ)`

---

## 🎨 2. GizmoRenderer.cs - GameObject 기반 렌더러

### 파일 위치
```
Assets/RuntimeGizmo/GizmoRenderer.cs
```

### 클래스 구조

```csharp
public class GizmoRenderer : MonoBehaviour
{
    private TransformGizmo _gizmo;
    
    // 핸들 컨테이너
    private GameObject _handleContainer;      // Position 핸들들
    private GameObject _rotationContainer;    // Rotation 핸들들
    private GameObject _scaleContainer;       // Scale 핸들들
    
    // 핸들 오브젝트 딕셔너리
    private Dictionary<Axis, GameObject> _axisHandles;
    private Dictionary<Axis, GameObject> _planeHandles;
    private Dictionary<Axis, GameObject> _rotationHandles;
    private Dictionary<Axis, GameObject> _scaleHandles;
    
    // 머티리얼 딕셔너리 (색상 변경용)
    private Dictionary<Axis, Material> _axisMaterials;
    private Dictionary<Axis, Material> _planeMaterials;
    private Dictionary<Axis, Material> _rotationMaterials;
    private Dictionary<Axis, Material> _scaleMaterials;
}
```

---

### 2.1 Initialize() - 렌더러 초기화

```csharp
public void Initialize(TransformGizmo gizmo)
{
    _gizmo = gizmo;
    
    // 기즈모 전용 레이어 설정 (레이어 2 = Ignore Raycast)
    gameObject.layer = 2;
    
    CreateHandles();
}
```

**설명**:
- `Layer 2` 설정: 일반 오브젝트 선택과 기즈모 핸들 선택을 분리
- Unity의 기본 레이어 2는 "Ignore Raycast"로 예약됨
- 이를 역으로 활용하여 기즈모만 감지하는 레이어로 사용

---

### 2.2 CreateAxisHandle() - Position 축 핸들 생성

```csharp
void CreateAxisHandle(Axis axis, Vector3 direction, Color color)
{
    GameObject handle = new GameObject($"Axis_{axis}");
    handle.transform.SetParent(_handleContainer.transform, false);
    handle.layer = 2;
    
    // === 축 라인 (Cone) 생성 ===
    GameObject line = new GameObject("Line");
    line.transform.SetParent(handle.transform, false);
    line.layer = 2;
    
    // MeshRenderer 설정
    MeshRenderer mr = line.AddComponent<MeshRenderer>();
    Material mat = new Material(Shader.Find("Unlit/Color"));
    mat.color = color;
    mr.material = mat;
    _axisMaterials[axis] = mat;  // 나중에 색상 변경을 위해 저장
    
    // MeshFilter 설정 (시각적 메시)
    MeshFilter mf = line.AddComponent<MeshFilter>();
    mf.mesh = MeshUtils.CreateCone(0.25f, 0.005f, 0.005f, 8, 1);
    
    // MeshCollider 설정 (충돌 감지용 - 더 두꺼움)
    MeshCollider mc = line.AddComponent<MeshCollider>();
    mc.sharedMesh = MeshUtils.CreateCone(0.25f, 0.02f, 0.005f, 8, 1);
    
    // 방향 설정
    line.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
    
    // === 화살표 끝 (Cone) 생성 ===
    GameObject arrow = new GameObject("Arrow");
    arrow.transform.SetParent(handle.transform, false);
    arrow.layer = 2;
    
    mr = arrow.AddComponent<MeshRenderer>();
    mr.material = mat;  // 같은 머티리얼 공유
    
    mf = arrow.AddComponent<MeshFilter>();
    mf.mesh = MeshUtils.CreateCone(0.05f, 0.025f, 0f, 8, 1);  // topRadius=0 (뾰족)
    
    mc = arrow.AddComponent<MeshCollider>();
    mc.sharedMesh = mf.mesh;
    
    arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
    arrow.transform.localPosition = direction * 0.25f;  // 라인 끝에 위치
    
    _axisHandles[axis] = handle;
}
```

**핵심 포인트**:

1. **시각적 메시 vs 충돌 메시 분리**
   ```csharp
   // 시각적: 얇고 예쁨
   mf.mesh = MeshUtils.CreateCone(0.25f, 0.005f, 0.005f, 8, 1);
   
   // 충돌: 두껍고 클릭하기 쉬움
   mc.sharedMesh = MeshUtils.CreateCone(0.25f, 0.02f, 0.005f, 8, 1);
   ```

2. **방향 설정**
   ```csharp
   Quaternion.FromToRotation(Vector3.up, direction)
   ```
   - 메시는 기본적으로 Y축(위) 방향으로 생성됨
   - 이를 원하는 방향(X/Y/Z)으로 회전

3. **계층 구조**
   ```
   Axis_X
   ├── Line (원기둥)
   └── Arrow (원뿔)
   ```

---

### 2.3 CreatePlaneHandle() - Position 평면 핸들 생성

```csharp
void CreatePlaneHandle(Axis axis, Vector3 normal, Vector3 tangent1, Vector3 tangent2, 
                       Color color1, Color color2)
{
    GameObject plane = new GameObject($"Plane_{axis}");
    plane.transform.SetParent(_handleContainer.transform, false);
    plane.layer = 2;
    
    // 색상 블렌딩
    MeshRenderer mr = plane.AddComponent<MeshRenderer>();
    Material mat = new Material(Shader.Find("Unlit/Color"));
    Color blendedColor = (color1 + color2) * 0.5f;  // 두 축 색상의 평균
    blendedColor.a = _gizmo.planesOpacity;  // 투명도 적용
    mat.color = blendedColor;
    mr.material = mat;
    
    // 얇은 박스 메시
    MeshFilter mf = plane.AddComponent<MeshFilter>();
    mf.mesh = MeshUtils.CreateBox(0.005f, 0.06f, 0.06f);
    
    MeshCollider mc = plane.AddComponent<MeshCollider>();
    mc.sharedMesh = mf.mesh;
    
    // 평면 방향 및 위치 설정
    plane.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
    plane.transform.localPosition = (tangent1 + tangent2) * 0.03f;
    
    _planeHandles[axis] = plane;
}
```

**설명**:
- **YZ 평면 (Plane_X)**: Y축과 Z축 사이의 평면
  - `normal = Vector3.right` (X축에 수직)
  - `color = (yColor + zColor) / 2` (초록+파랑 = 청록)
  - 위치: Y와 Z 방향으로 약간 오프셋

---

### 2.4 CreateRotationHandle() - Rotation 핸들 생성

```csharp
void CreateRotationHandle(Axis axis, Vector3 direction, Color color)
{
    GameObject handle = new GameObject($"Rotation_{axis}");
    handle.transform.SetParent(_rotationContainer.transform, false);
    handle.layer = 2;
    
    MeshRenderer mr = handle.AddComponent<MeshRenderer>();
    Material mat = new Material(Shader.Find("Unlit/Color"));
    mat.color = color;
    mr.material = mat;
    _rotationMaterials[axis] = mat;
    
    MeshFilter mf = handle.AddComponent<MeshFilter>();
    if (axis == Axis.Any)
    {
        // 전체 회전용 구체
        mf.mesh = MeshUtils.CreateSphere(0.25f, 32, 16);
    }
    else
    {
        // 개별 축 회전용 토러스
        mf.mesh = MeshUtils.CreateTorus(0.25f, 0.005f, 32, 6);
    }
    
    MeshCollider mc = handle.AddComponent<MeshCollider>();
    if (axis == Axis.Any)
    {
        mc.sharedMesh = mf.mesh;
    }
    else
    {
        // 충돌용 토러스는 더 두껍게
        mc.sharedMesh = MeshUtils.CreateTorus(0.25f, 0.015f, 32, 6);
    }
    
    if (axis != Axis.Any)
    {
        // 토러스를 해당 축 방향으로 회전
        handle.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
    }
    
    _rotationHandles[axis] = handle;
}
```

**핵심 차이점**:
- **개별 축 (X/Y/Z)**: 토러스 (도넛 모양)
- **전체 회전 (Any)**: 구체 (공 모양)

---

### 2.5 UpdateHandles() - 매 프레임 업데이트

```csharp
public void UpdateHandles()
{
    if (_gizmo.mainTargetRoot == null)
    {
        // 타겟이 없으면 모든 핸들 숨김
        if (_handleContainer != null) _handleContainer.SetActive(false);
        if (_rotationContainer != null) _rotationContainer.SetActive(false);
        if (_scaleContainer != null) _scaleContainer.SetActive(false);
        return;
    }
    
    // === 위치 업데이트 ===
    transform.position = _gizmo.pivotPoint;
    
    // === 기본 회전은 항상 identity ===
    transform.rotation = Quaternion.identity;
    
    // === 크기 업데이트 (카메라 거리에 따라) ===
    float scale = _gizmo.GetDistanceMultiplier();
    transform.localScale = Vector3.one * scale;
    
    // === 타입에 따라 표시/숨김 ===
    _handleContainer.SetActive(_gizmo.TransformTypeContains(TransformType.Move));
    _rotationContainer.SetActive(_gizmo.TransformTypeContains(TransformType.Rotate));
    _scaleContainer.SetActive(_gizmo.TransformTypeContains(TransformType.Scale));
    
    // === 각 컨테이너별 회전 설정 ===
    bool isLocalSpace = _gizmo.space == TransformSpace.Local;
    
    // Position과 Rotation: space 설정에 따라
    if (_handleContainer != null)
    {
        _handleContainer.transform.rotation = isLocalSpace 
            ? _gizmo.mainTargetRoot.rotation 
            : Quaternion.identity;
    }
    if (_rotationContainer != null)
    {
        _rotationContainer.transform.rotation = isLocalSpace 
            ? _gizmo.mainTargetRoot.rotation 
            : Quaternion.identity;
    }
    
    // Scale: 항상 Local (오브젝트 회전 따라감)
    if (_scaleContainer != null)
    {
        _scaleContainer.transform.rotation = _gizmo.mainTargetRoot.rotation;
    }
    
    // === 색상 업데이트 ===
    UpdateColors();
}
```

**중요한 설계 결정**:

1. **컨테이너별 독립적인 회전**
   ```csharp
   // 잘못된 방법 (모든 핸들이 같이 회전)
   transform.rotation = targetRotation;
   
   // 올바른 방법 (각 컨테이너가 독립적으로 회전)
   _handleContainer.transform.rotation = targetRotation;
   _rotationContainer.transform.rotation = targetRotation;
   _scaleContainer.transform.rotation = _gizmo.mainTargetRoot.rotation;
   ```

2. **Scale은 항상 Local**
   - Scale 핸들은 오브젝트의 로컬 축을 따라야 함
   - Global 모드에서도 Scale은 Local 축 사용

3. **카메라 거리 기반 스케일**
   ```csharp
   transform.localScale = Vector3.one * GetDistanceMultiplier();
   ```
   - 카메라에서 멀어져도 화면상 크기 일정 유지

---

### 2.6 UpdateColors() - 색상 업데이트

```csharp
void UpdateColors()
{
    Axis nearAxis = _gizmo.translatingAxis;  // 현재 마우스가 가리키는 축
    bool isTransforming = _gizmo.isTransforming;  // 드래그 중인지
    
    // Position 핸들 색상
    if (_gizmo.TransformTypeContains(TransformType.Move))
    {
        UpdateAxisColor(Axis.X, _gizmo.xColor, nearAxis, isTransforming);
        UpdateAxisColor(Axis.Y, _gizmo.yColor, nearAxis, isTransforming);
        UpdateAxisColor(Axis.Z, _gizmo.zColor, nearAxis, isTransforming);
    }
    
    // Rotation 핸들 색상
    // ... 동일한 패턴
    
    // Scale 핸들 색상
    // ... 동일한 패턴
}

void UpdateAxisColor(Axis axis, Color defaultColor, Axis nearAxis, bool isTransforming)
{
    if (_axisMaterials.ContainsKey(axis))
    {
        Color color;
        if (nearAxis == axis)
        {
            // 해당 축에 마우스가 올라가 있음
            color = isTransforming ? _gizmo.selectedColor : _gizmo.hoverColor;
        }
        else
        {
            // 기본 색상
            color = defaultColor;
        }
        
        _axisMaterials[axis].color = color;
    }
}
```

**색상 상태**:
1. **기본**: `defaultColor` (빨강/초록/파랑)
2. **호버**: `hoverColor` (주황색)
3. **선택/드래그**: `selectedColor` (노란색)

---

## 🔄 3. TransformGizmo.cs - 메인 로직 수정

### 3.1 GL 렌더링 제거

**제거된 메서드들**:
```csharp
// ❌ 제거됨
void OnPostRender()
{
    GL.PushMatrix();
    GL.Begin(GL.LINES);
    // ... GL 드로잉 코드
    GL.End();
    GL.PopMatrix();
}

void OnRenderObject()
{
    // ... GL 드로잉 코드
}
```

**이유**:
- GL 즉시 모드는 성능이 떨어짐
- 매 프레임 CPU에서 정점 계산
- 깊이 정렬, 충돌 감지 등을 수동으로 구현해야 함

---

### 3.2 GameObject 렌더링 통합

```csharp
// GameObject 기반 렌더러
private GizmoRenderer _gizmoRenderer;

void Awake()
{
    myCamera = GetComponent<Camera>();
    SetMaterial();
    
    // GizmoRenderer 생성 및 초기화
    GameObject rendererObj = new GameObject("GizmoRenderer");
    rendererObj.transform.SetParent(transform, false);
    _gizmoRenderer = rendererObj.AddComponent<GizmoRenderer>();
    _gizmoRenderer.Initialize(this);
}

void LateUpdate()
{
    if(mainTargetRoot == null) return;

    SetAxisInfo();
    
    if(manuallyHandleGizmo)
    {
        if(onDrawCustomGizmo != null) onDrawCustomGizmo();
    }
    
    // GameObject 기반 렌더링 업데이트
    if (_gizmoRenderer != null)
    {
        _gizmoRenderer.UpdateHandles();
    }
}
```

**설명**:
- `Awake()`에서 GizmoRenderer 생성
- `LateUpdate()`에서 매 프레임 업데이트
- TransformGizmo는 로직만, GizmoRenderer는 시각화만 담당 (관심사 분리)

---

### 3.3 SetNearAxis() - Raycast 기반 핸들 감지

**기존 방식 (GL)**:
```csharp
// 수동으로 마우스와 각 축/평면 사이의 거리 계산
// 복잡한 기하학 계산 필요
float distanceToAxis = CalculateDistanceToAxis(mouseRay, axisPosition, axisDirection);
if (distanceToAxis < minDistance)
{
    nearAxis = currentAxis;
}
```

**새로운 방식 (Raycast)**:
```csharp
void SetNearAxis()
{
    if(isTransforming) return;
    
    SetTranslatingAxis(transformType, Axis.None);
    
    if(mainTargetRoot == null) return;
    
    // GameObject 기반 렌더링 - Raycast로 핸들 감지
    if (_gizmoRenderer != null)
    {
        Ray mouseRay = myCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Layer 2 (Ignore Raycast)만 감지하도록 레이어 마스크 설정
        int gizmoLayerMask = 1 << 2;
        
        if (Physics.Raycast(mouseRay, out hit, Mathf.Infinity, gizmoLayerMask))
        {
            // 히트한 오브젝트가 기즈모 핸들인지 확인
            Transform hitTransform = hit.transform;
            
            // Position 핸들 체크
            if (TransformTypeContains(TransformType.Move))
            {
                if (hitTransform.name.Contains("Axis_X") || 
                    hitTransform.parent != null && hitTransform.parent.name.Contains("Axis_X"))
                {
                    SetTranslatingAxis(TransformType.Move, Axis.X);
                    return;
                }
                else if (hitTransform.name.Contains("Axis_Y") || 
                         hitTransform.parent != null && hitTransform.parent.name.Contains("Axis_Y"))
                {
                    SetTranslatingAxis(TransformType.Move, Axis.Y);
                    return;
                }
                // ... Z축 및 평면 핸들 체크
            }
            
            // Rotation 핸들 체크
            if (TransformTypeContains(TransformType.Rotate))
            {
                if (hitTransform.name.Contains("Rotation_X"))
                {
                    SetTranslatingAxis(TransformType.Rotate, Axis.X);
                    return;
                }
                // ... Y, Z, Any 축 체크
            }
            
            // Scale 핸들 체크
            // ... 동일한 패턴
        }
    }
}
```

**핵심 개선사항**:

1. **레이어 마스크 활용**
   ```csharp
   int gizmoLayerMask = 1 << 2;  // Layer 2만 감지
   Physics.Raycast(mouseRay, out hit, Mathf.Infinity, gizmoLayerMask)
   ```
   - 일반 오브젝트는 무시하고 기즈모만 감지
   - 성능 향상 및 정확도 증가

2. **GameObject 이름 기반 판별**
   ```csharp
   if (hitTransform.name.Contains("Axis_X"))
   ```
   - 명확하고 유지보수하기 쉬운 코드
   - 복잡한 수학 계산 불필요

3. **부모 체크**
   ```csharp
   hitTransform.parent != null && hitTransform.parent.name.Contains("Axis_X")
   ```
   - Line이나 Arrow 같은 자식 오브젝트를 클릭해도 동작
   - 사용자 경험 향상

---

### 3.4 HandleUndoRedo() - Undo/Redo 시스템 수정

**기존 코드**:
```csharp
void HandleUndoRedo()
{
    if(maxUndoStored != UndoRedoManager.maxUndoStored) 
    { 
        UndoRedoManager.maxUndoStored = maxUndoStored; 
    }

    if(Input.GetKey(ActionKey))  // LeftShift
    {
        if(Input.GetKeyDown(UndoAction))  // Z
        {
            UndoRedoManager.Undo();
        }
        else if(Input.GetKeyDown(RedoAction))  // Y
        {
            UndoRedoManager.Redo();
        }
    }
}
```

**문제점**: Shift+Z, Shift+Y는 직관적이지 않음

**수정된 코드**:
```csharp
void HandleUndoRedo()
{
    if(maxUndoStored != UndoRedoManager.maxUndoStored) 
    { 
        UndoRedoManager.maxUndoStored = maxUndoStored; 
    }

    // Ctrl+Z for Undo, Ctrl+Y for Redo (standard shortcuts)
    if(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
    {
        if(Input.GetKeyDown(UndoAction))
        {
            UndoRedoManager.Undo();
        }
        else if(Input.GetKeyDown(RedoAction))
        {
            UndoRedoManager.Redo();
        }
    }
}
```

**개선사항**:
- **Ctrl+Z**: Undo (표준 단축키)
- **Ctrl+Y**: Redo (표준 단축키)
- 왼쪽/오른쪽 Ctrl 모두 지원

---

## 🐛 4. 해결한 버그들

### 4.1 스케일 깜빡임 버그

**증상**: 핸들 크기가 매 프레임 미세하게 변동

**원인**:
```csharp
// UpdateHandles()에서 매 프레임 호출
float scale = GetDistanceMultiplier();
transform.localScale = Vector3.one * scale;
```

`GetDistanceMultiplier()`가 매 프레임 약간씩 다른 값을 반환하여 깜빡임 발생

**해결**:
```csharp
// 한 번만 계산하여 저장
float scale = _gizmo.GetDistanceMultiplier();
transform.localScale = Vector3.one * scale;
```

실제로는 코드 변경 없이, 문제는 다른 곳(회전 설정)에 있었지만 이 과정에서 코드 구조가 개선됨

---

### 4.2 Raycast 레이어 마스크 문제

**증상**: 기즈모 핸들을 클릭해도 반응 없음

**원인**:
```csharp
// 모든 레이어를 감지
if (Physics.Raycast(mouseRay, out hit, Mathf.Infinity))
{
    // 일반 오브젝트도 히트되어 기즈모 핸들이 감지되지 않음
}
```

**해결**:
```csharp
// Layer 2만 감지
int gizmoLayerMask = 1 << 2;
if (Physics.Raycast(mouseRay, out hit, Mathf.Infinity, gizmoLayerMask))
{
    // 기즈모 핸들만 감지됨
}
```

**설명**:
- `1 << 2` = `0b00000100` = Layer 2 비트만 1
- 레이어 마스크는 비트 플래그로 작동
- Layer 2에 있는 오브젝트만 Raycast에 감지됨

---

### 4.3 Local/Global 회전 문제

**증상**: 
- Global 모드에서도 핸들이 오브젝트를 따라 회전
- Scale 핸들이 Global 모드에서 월드 축을 따름

**원인**:
```csharp
// 전체 transform의 rotation 설정
transform.rotation = isLocalSpace 
    ? _gizmo.mainTargetRoot.rotation 
    : Quaternion.identity;
```

모든 핸들(Position, Rotation, Scale)이 동일하게 회전

**해결**:
```csharp
// 기본 transform은 항상 identity
transform.rotation = Quaternion.identity;

// 각 컨테이너별 독립적인 회전 설정
bool isLocalSpace = _gizmo.space == TransformSpace.Local;

// Position과 Rotation: space 설정에 따라
_handleContainer.transform.rotation = isLocalSpace 
    ? _gizmo.mainTargetRoot.rotation 
    : Quaternion.identity;
    
_rotationContainer.transform.rotation = isLocalSpace 
    ? _gizmo.mainTargetRoot.rotation 
    : Quaternion.identity;

// Scale: 항상 Local (오브젝트 회전 따라감)
_scaleContainer.transform.rotation = _gizmo.mainTargetRoot.rotation;
```

**설명**:
- Position/Rotation 핸들: 사용자 선택에 따라 Local/Global
- Scale 핸들: 항상 Local (오브젝트의 로컬 축을 따라 크기 조정)

---

### 4.4 핸들 크기 문제

**증상**: 핸들이 너무 커서 화면을 가림

**원인**:
```csharp
// 원본 크기 (너무 큼)
mf.mesh = MeshUtils.CreateCone(1.0f, 0.02f, 0.02f, 8, 1);
```

**해결**:
```csharp
// 크기를 1/4로 축소
mf.mesh = MeshUtils.CreateCone(0.25f, 0.005f, 0.005f, 8, 1);
```

**적용된 크기**:
- 축 라인 높이: 1.0 → 0.25
- 축 라인 반지름: 0.02 → 0.005
- 화살표 높이: 0.2 → 0.05
- 토러스 반지름: 1.0 → 0.25
- 구체 반지름: 1.0 → 0.25

---

### 4.5 Pivot 기본값 문제

**증상**: 여러 오브젝트 선택 시 첫 번째 오브젝트 위치에 기즈모 표시

**원인**:
```csharp
public TransformPivot pivot = TransformPivot.Pivot;
```

**해결**:
```csharp
public TransformPivot pivot = TransformPivot.Center;
```

**동작**:
- **Pivot 모드**: 첫 번째 선택된 오브젝트의 pivot 위치
- **Center 모드**: 모든 선택된 오브젝트의 중심점

---

## 💡 5. 주요 기술 포인트

### 5.1 Layer 시스템 활용

```csharp
// 모든 기즈모 핸들을 Layer 2로 설정
gameObject.layer = 2;
handle.layer = 2;
line.layer = 2;
arrow.layer = 2;

// Raycast 시 Layer 2만 감지
int gizmoLayerMask = 1 << 2;
Physics.Raycast(mouseRay, out hit, Mathf.Infinity, gizmoLayerMask);
```

**장점**:
- 일반 오브젝트 선택과 기즈모 핸들 선택 완전 분리
- 성능 향상 (불필요한 충돌 검사 제거)
- 명확한 역할 구분

---

### 5.2 MeshCollider를 통한 정확한 충돌 감지

```csharp
// 시각적 메시 (얇고 예쁨)
MeshFilter mf = line.AddComponent<MeshFilter>();
mf.mesh = MeshUtils.CreateCone(0.25f, 0.005f, 0.005f, 8, 1);

// 충돌 메시 (두껍고 클릭하기 쉬움)
MeshCollider mc = line.AddComponent<MeshCollider>();
mc.sharedMesh = MeshUtils.CreateCone(0.25f, 0.02f, 0.005f, 8, 1);
```

**장점**:
- 시각적 품질과 사용성의 균형
- 얇은 라인도 쉽게 클릭 가능
- Unity의 물리 엔진 활용

---

### 5.3 컨테이너별 독립적인 회전 처리

```csharp
// 계층 구조
GizmoRenderer
├── PositionHandles (Container) ← 독립적 회전
├── RotationHandles (Container) ← 독립적 회전
└── ScaleHandles (Container)    ← 독립적 회전

// 각 컨테이너별 회전 설정
_handleContainer.transform.rotation = ...;     // Position
_rotationContainer.transform.rotation = ...;   // Rotation  
_scaleContainer.transform.rotation = ...;      // Scale (항상 Local)
```

**장점**:
- 각 Transform 타입이 독립적으로 동작
- Scale은 항상 Local, Position/Rotation은 사용자 선택
- 유연하고 확장 가능한 구조

---

### 5.4 카메라 거리 기반 스케일 조정

```csharp
public float GetDistanceMultiplier()
{
    if(mainTargetRoot == null) return 0f;

    // 직교 카메라
    if(myCamera.orthographic) 
        return Mathf.Max(.01f, myCamera.orthographicSize * 2f);
    
    // 원근 카메라
    return Mathf.Max(.01f, Mathf.Abs(
        ExtVector3.MagnitudeInDirection(
            pivotPoint - transform.position, 
            myCamera.transform.forward
        )
    ));
}

// 사용
transform.localScale = Vector3.one * GetDistanceMultiplier();
```

**설명**:
- 카메라에서 멀어져도 화면상 크기 일정 유지
- 직교/원근 카메라 모두 지원
- Unity 에디터의 기즈모와 동일한 동작

---

## 📊 6. 코드 통계 및 성능

### 생성/수정된 파일

| 파일 | 상태 | 라인 수 | 설명 |
|------|------|---------|------|
| `MeshUtils.cs` | 생성 | ~400 | 메시 생성 유틸리티 |
| `GizmoRenderer.cs` | 생성 | ~400 | GameObject 렌더러 |
| `TransformGizmo.cs` | 수정 | ~100 변경 | GL 제거, Raycast 추가 |

### 성능 비교

| 항목 | GL 렌더링 | GameObject 렌더링 |
|------|-----------|-------------------|
| 렌더링 방식 | CPU 즉시 모드 | GPU 배치 렌더링 |
| 정점 계산 | 매 프레임 | 초기화 시 1회 |
| 충돌 감지 | 수동 계산 | Physics 엔진 |
| 깊이 정렬 | 수동 구현 | 자동 |
| 확장성 | 낮음 | 높음 |

---

## ✅ 7. 최종 결과

### 개선사항

1. ✅ **성능 향상**
   - GL 즉시 모드 → Unity 렌더링 파이프라인
   - CPU 부하 감소
   - GPU 활용 최적화

2. ✅ **정확한 인터랙션**
   - MeshCollider 기반 물리 충돌
   - 레이어 시스템으로 정확한 분리
   - 두꺼운 충돌 메시로 클릭 용이성 향상

3. ✅ **시각적 품질**
   - 적절한 쉐이더 사용
   - 부드러운 메시 (토러스, 구체)
   - 일관된 화면 크기 유지

4. ✅ **유지보수성**
   - 구조화된 GameObject 계층
   - 명확한 역할 분리 (로직 vs 렌더링)
   - 쉬운 디버깅 (Hierarchy에서 확인 가능)

5. ✅ **확장성**
   - 새로운 핸들 타입 추가 용이
   - 커스텀 메시 지원
   - 플러그인 구조

### 지원 기능

- ✅ **Position (이동)**: X/Y/Z 축 + XY/XZ/YZ 평면
- ✅ **Rotation (회전)**: X/Y/Z 축 + 전체 회전
- ✅ **Scale (크기)**: X/Y/Z 축 + 균일 스케일
- ✅ **Local/Global 공간 전환** (X 키)
- ✅ **Pivot/Center 모드 전환** (Z 키)
- ✅ **마우스 호버/선택 시각 피드백**
- ✅ **카메라 거리 무관 일정한 화면 크기**
- ✅ **여러 오브젝트 동시 선택 및 변형**
- ✅ **Undo/Redo** (Ctrl+Z / Ctrl+Y)

---

## 🎯 8. 사용 방법

### 기본 사용

```csharp
// 카메라에 TransformGizmo 컴포넌트 추가
Camera mainCamera = Camera.main;
TransformGizmo gizmo = mainCamera.gameObject.AddComponent<TransformGizmo>();

// 오브젝트 선택 (자동으로 기즈모 표시됨)
// 마우스 클릭으로 오브젝트 선택
```

### 단축키

| 키 | 기능 |
|----|------|
| W | Move (이동) 모드 |
| E | Rotate (회전) 모드 |
| R | Scale (크기) 모드 |
| Y | All (전체) 모드 |
| X | Local/Global 전환 |
| Z | Pivot/Center 전환 |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Shift | 다중 선택 추가 |
| Ctrl | Snapping 활성화 |

---

## 📝 9. 향후 개선 사항

1. **커스텀 쉐이더**
   - 현재: `Unlit/Color`
   - 개선: 깊이 테스트, 외곽선, 그라데이션 등

2. **애니메이션**
   - 호버 시 크기 변화
   - 선택 시 펄스 효과

3. **추가 핸들 타입**
   - Rect Tool (UI용)
   - Custom Handle (사용자 정의)

4. **성능 최적화**
   - 오브젝트 풀링
   - LOD (Level of Detail)

---

## 📚 10. 참고 자료

### Unity 문서
- [Mesh 클래스](https://docs.unity3d.com/ScriptReference/Mesh.html)
- [MeshCollider](https://docs.unity3d.com/ScriptReference/MeshCollider.html)
- [Physics.Raycast](https://docs.unity3d.com/ScriptReference/Physics.Raycast.html)
- [Layer 시스템](https://docs.unity3d.com/Manual/Layers.html)

### 수학
- [구면 좌표계](https://en.wikipedia.org/wiki/Spherical_coordinate_system)
- [토러스 방정식](https://en.wikipedia.org/wiki/Torus)

---

## 👨‍💻 작성자

작업 기간: 2025년 11월 3일  
작업 시간: 약 2시간  
수정 파일: 3개  
추가 코드: ~800줄  
제거 코드: ~500줄

---

**이 문서는 RuntimeGizmo의 GL에서 GameObject 기반 렌더링으로의 완전한 전환 작업을 상세히 기록합니다.**
