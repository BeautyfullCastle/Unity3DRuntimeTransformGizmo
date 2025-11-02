using UnityEngine;
using System.Collections.Generic;

namespace RuntimeGizmos
{
	/// <summary>
	/// GameObject 기반 기즈모 렌더러
	/// RuntimeTransformHandle의 렌더링 시스템을 참고하여 구현
	/// </summary>
	public class GizmoRenderer : MonoBehaviour
	{
		private TransformGizmo _gizmo;
		
		// 핸들 오브젝트들
		private GameObject _handleContainer;
		private Dictionary<Axis, GameObject> _axisHandles = new Dictionary<Axis, GameObject>();
		private Dictionary<Axis, GameObject> _planeHandles = new Dictionary<Axis, GameObject>();
		private Dictionary<Axis, Material> _axisMaterials = new Dictionary<Axis, Material>();
		private Dictionary<Axis, Material> _planeMaterials = new Dictionary<Axis, Material>();
		
		// 회전 핸들
		private GameObject _rotationContainer;
		private Dictionary<Axis, GameObject> _rotationHandles = new Dictionary<Axis, GameObject>();
		private Dictionary<Axis, Material> _rotationMaterials = new Dictionary<Axis, Material>();
		
		// 스케일 핸들
		private GameObject _scaleContainer;
		private Dictionary<Axis, GameObject> _scaleHandles = new Dictionary<Axis, GameObject>();
		private Dictionary<Axis, Material> _scaleMaterials = new Dictionary<Axis, Material>();
		
		public void Initialize(TransformGizmo gizmo)
		{
			_gizmo = gizmo;
			
			// 기즈모 전용 레이어 설정 (레이어 2 = Ignore Raycast)
			gameObject.layer = 2;
			
			CreateHandles();
		}
		
		void CreateHandles()
		{
			// 메인 컨테이너 생성
			_handleContainer = new GameObject("PositionHandles");
			_handleContainer.transform.SetParent(transform, false);
			
			_rotationContainer = new GameObject("RotationHandles");
			_rotationContainer.transform.SetParent(transform, false);
			
			_scaleContainer = new GameObject("ScaleHandles");
			_scaleContainer.transform.SetParent(transform, false);
			
			CreatePositionHandles();
			CreateRotationHandles();
			CreateScaleHandles();
		}
		
		void CreatePositionHandles()
		{
			// X축 핸들
			CreateAxisHandle(Axis.X, Vector3.right, _gizmo.xColor);
			CreatePlaneHandle(Axis.X, Vector3.right, Vector3.up, Vector3.forward, _gizmo.yColor, _gizmo.zColor);
			
			// Y축 핸들
			CreateAxisHandle(Axis.Y, Vector3.up, _gizmo.yColor);
			CreatePlaneHandle(Axis.Y, Vector3.up, Vector3.right, Vector3.forward, _gizmo.xColor, _gizmo.zColor);
			
			// Z축 핸들
			CreateAxisHandle(Axis.Z, Vector3.forward, _gizmo.zColor);
			CreatePlaneHandle(Axis.Z, Vector3.forward, Vector3.right, Vector3.up, _gizmo.xColor, _gizmo.yColor);
		}
		
		void CreateAxisHandle(Axis axis, Vector3 direction, Color color)
		{
			GameObject handle = new GameObject($"Axis_{axis}");
			handle.transform.SetParent(_handleContainer.transform, false);
			handle.layer = 2; // Ignore Raycast 레이어
			
			// 축 라인 (Cone) - 크기 축소
			GameObject line = new GameObject("Line");
			line.transform.SetParent(handle.transform, false);
			line.layer = 2;
			
			MeshRenderer mr = line.AddComponent<MeshRenderer>();
			Material mat = new Material(Shader.Find("Unlit/Color"));
			mat.color = color;
			mr.material = mat;
			_axisMaterials[axis] = mat;
			
			MeshFilter mf = line.AddComponent<MeshFilter>();
			mf.mesh = MeshUtils.CreateCone(0.25f, 0.005f, 0.005f, 8, 1);
			
			MeshCollider mc = line.AddComponent<MeshCollider>();
			mc.sharedMesh = MeshUtils.CreateCone(0.25f, 0.02f, 0.005f, 8, 1);
			
			line.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
			
			// 끝 화살표 (Cone) - 크기 축소
			GameObject arrow = new GameObject("Arrow");
			arrow.transform.SetParent(handle.transform, false);
			arrow.layer = 2;
			
			mr = arrow.AddComponent<MeshRenderer>();
			mr.material = mat;
			
			mf = arrow.AddComponent<MeshFilter>();
			mf.mesh = MeshUtils.CreateCone(0.05f, 0.025f, 0f, 8, 1);
			
			mc = arrow.AddComponent<MeshCollider>();
			mc.sharedMesh = mf.mesh;
			
			arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
			arrow.transform.localPosition = direction * 0.25f;
			
			_axisHandles[axis] = handle;
		}
		
		void CreatePlaneHandle(Axis axis, Vector3 normal, Vector3 tangent1, Vector3 tangent2, Color color1, Color color2)
		{
			GameObject plane = new GameObject($"Plane_{axis}");
			plane.transform.SetParent(_handleContainer.transform, false);
			plane.layer = 2;
			
			MeshRenderer mr = plane.AddComponent<MeshRenderer>();
			Material mat = new Material(Shader.Find("Unlit/Color"));
			Color blendedColor = (color1 + color2) * 0.5f;
			blendedColor.a = _gizmo.planesOpacity;
			mat.color = blendedColor;
			mr.material = mat;
			_planeMaterials[axis] = mat;
			
			MeshFilter mf = plane.AddComponent<MeshFilter>();
			mf.mesh = MeshUtils.CreateBox(0.005f, 0.06f, 0.06f);
			
			MeshCollider mc = plane.AddComponent<MeshCollider>();
			mc.sharedMesh = mf.mesh;
			
			plane.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
			plane.transform.localPosition = (tangent1 + tangent2) * 0.03f;
			
			_planeHandles[axis] = plane;
		}
		
		void CreateRotationHandles()
		{
			// X축 회전 (Torus - 빨강)
			CreateRotationHandle(Axis.X, Vector3.right, _gizmo.xColor);
			
			// Y축 회전 (Torus - 초록)
			CreateRotationHandle(Axis.Y, Vector3.up, _gizmo.yColor);
			
			// Z축 회전 (Torus - 파랑)
			CreateRotationHandle(Axis.Z, Vector3.forward, _gizmo.zColor);
			
			// All 회전 (Sphere - 회색)
			CreateRotationHandle(Axis.Any, Vector3.up, _gizmo.allColor);
		}
		
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
				// 전체 회전용 구체 - 크기 축소
				mf.mesh = MeshUtils.CreateSphere(0.25f, 32, 16);
			}
			else
			{
				// 개별 축 회전용 토러스 - 크기 축소
				mf.mesh = MeshUtils.CreateTorus(0.25f, 0.005f, 32, 6);
			}
			
			MeshCollider mc = handle.AddComponent<MeshCollider>();
			if (axis == Axis.Any)
			{
				mc.sharedMesh = mf.mesh;
			}
			else
			{
				mc.sharedMesh = MeshUtils.CreateTorus(0.25f, 0.015f, 32, 6);
			}
			
			if (axis != Axis.Any)
			{
				handle.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
			}
			
			_rotationHandles[axis] = handle;
		}
		
		void CreateScaleHandles()
		{
			// X축 스케일
			CreateScaleHandle(Axis.X, Vector3.right, _gizmo.xColor);
			
			// Y축 스케일
			CreateScaleHandle(Axis.Y, Vector3.up, _gizmo.yColor);
			
			// Z축 스케일
			CreateScaleHandle(Axis.Z, Vector3.forward, _gizmo.zColor);
			
			// 전체 스케일
			CreateScaleHandle(Axis.Any, Vector3.zero, _gizmo.allColor);
		}
		
		void CreateScaleHandle(Axis axis, Vector3 direction, Color color)
		{
			GameObject handle = new GameObject($"Scale_{axis}");
			handle.transform.SetParent(_scaleContainer.transform, false);
			handle.layer = 2;
			
			// 축 라인 - 크기 축소
			if (axis != Axis.Any)
			{
				GameObject line = new GameObject("Line");
				line.transform.SetParent(handle.transform, false);
				line.layer = 2;
				
				MeshRenderer mr = line.AddComponent<MeshRenderer>();
				Material mat = new Material(Shader.Find("Unlit/Color"));
				mat.color = color;
				mr.material = mat;
				
				MeshFilter mf = line.AddComponent<MeshFilter>();
				mf.mesh = MeshUtils.CreateCone(0.25f, 0.005f, 0.005f, 8, 1);
				
				MeshCollider mc = line.AddComponent<MeshCollider>();
				mc.sharedMesh = MeshUtils.CreateCone(0.25f, 0.02f, 0.005f, 8, 1);
				
				line.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
			}
			
			// 끝 큐브 - 크기 축소
			GameObject cube = new GameObject("Cube");
			cube.transform.SetParent(handle.transform, false);
			cube.layer = 2;
			
			MeshRenderer cubeMr = cube.AddComponent<MeshRenderer>();
			Material cubeMat = new Material(Shader.Find("Unlit/Color"));
			cubeMat.color = color;
			cubeMr.material = cubeMat;
			_scaleMaterials[axis] = cubeMat;
			
			MeshFilter cubeMf = cube.AddComponent<MeshFilter>();
			cubeMf.mesh = MeshUtils.CreateBox(0.02f, 0.02f, 0.02f);
			
			MeshCollider cubeMc = cube.AddComponent<MeshCollider>();
			cubeMc.sharedMesh = cubeMf.mesh;
			
			if (axis != Axis.Any)
			{
				cube.transform.localPosition = direction * 0.25f;
			}
			
			_scaleHandles[axis] = handle;
		}
		
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
			
			// 위치 업데이트
			transform.position = _gizmo.pivotPoint;
			
			// 기본 회전은 항상 identity (각 컨테이너가 개별적으로 회전 설정)
			transform.rotation = Quaternion.identity;
			
			// 크기 업데이트 (카메라 거리에 따라)
			float scale = _gizmo.GetDistanceMultiplier();
			transform.localScale = Vector3.one * scale;
			
			// 타입에 따라 표시/숨김
			_handleContainer.SetActive(_gizmo.TransformTypeContains(TransformType.Move));
			_rotationContainer.SetActive(_gizmo.TransformTypeContains(TransformType.Rotate));
			_scaleContainer.SetActive(_gizmo.TransformTypeContains(TransformType.Scale));
			
			// 각 컨테이너별 회전 설정
			bool isLocalSpace = _gizmo.space == TransformSpace.Local;
			
			// Position과 Rotation은 space 설정에 따라 회전
			if (_handleContainer != null)
			{
				_handleContainer.transform.rotation = isLocalSpace ? _gizmo.mainTargetRoot.rotation : Quaternion.identity;
			}
			if (_rotationContainer != null)
			{
				_rotationContainer.transform.rotation = isLocalSpace ? _gizmo.mainTargetRoot.rotation : Quaternion.identity;
			}
			// Scale은 항상 Local (오브젝트 회전 따라감)
			if (_scaleContainer != null)
			{
				_scaleContainer.transform.rotation = _gizmo.mainTargetRoot.rotation;
			}
			
			// 색상 업데이트
			UpdateColors();
		}
		
		void UpdateColors()
		{
			Axis nearAxis = _gizmo.translatingAxis;
			bool isTransforming = _gizmo.isTransforming;
			
			// Position 핸들 색상
			if (_gizmo.TransformTypeContains(TransformType.Move))
			{
				UpdateAxisColor(Axis.X, _gizmo.xColor, nearAxis, isTransforming);
				UpdateAxisColor(Axis.Y, _gizmo.yColor, nearAxis, isTransforming);
				UpdateAxisColor(Axis.Z, _gizmo.zColor, nearAxis, isTransforming);
			}
			
			// Rotation 핸들 색상
			if (_gizmo.TransformTypeContains(TransformType.Rotate))
			{
				UpdateRotationColor(Axis.X, _gizmo.xColor, nearAxis, isTransforming);
				UpdateRotationColor(Axis.Y, _gizmo.yColor, nearAxis, isTransforming);
				UpdateRotationColor(Axis.Z, _gizmo.zColor, nearAxis, isTransforming);
				UpdateRotationColor(Axis.Any, _gizmo.allColor, nearAxis, isTransforming);
			}
			
			// Scale 핸들 색상
			if (_gizmo.TransformTypeContains(TransformType.Scale))
			{
				UpdateScaleColor(Axis.X, _gizmo.xColor, nearAxis, isTransforming);
				UpdateScaleColor(Axis.Y, _gizmo.yColor, nearAxis, isTransforming);
				UpdateScaleColor(Axis.Z, _gizmo.zColor, nearAxis, isTransforming);
				UpdateScaleColor(Axis.Any, _gizmo.allColor, nearAxis, isTransforming);
			}
		}
		
		void UpdateAxisColor(Axis axis, Color defaultColor, Axis nearAxis, bool isTransforming)
		{
			if (_axisMaterials.ContainsKey(axis))
			{
				Color color = (nearAxis == axis) 
					? (isTransforming ? _gizmo.selectedColor : _gizmo.hoverColor) 
					: defaultColor;
				_axisMaterials[axis].color = color;
			}
		}
		
		void UpdateRotationColor(Axis axis, Color defaultColor, Axis nearAxis, bool isTransforming)
		{
			if (_rotationMaterials.ContainsKey(axis))
			{
				Color color = (nearAxis == axis) 
					? (isTransforming ? _gizmo.selectedColor : _gizmo.hoverColor) 
					: defaultColor;
				_rotationMaterials[axis].color = color;
			}
		}
		
		void UpdateScaleColor(Axis axis, Color defaultColor, Axis nearAxis, bool isTransforming)
		{
			if (_scaleMaterials.ContainsKey(axis))
			{
				Color color = (nearAxis == axis) 
					? (isTransforming ? _gizmo.selectedColor : _gizmo.hoverColor) 
					: defaultColor;
				_scaleMaterials[axis].color = color;
			}
		}
		
		public void Cleanup()
		{
			if (_handleContainer != null) Destroy(_handleContainer);
			if (_rotationContainer != null) Destroy(_rotationContainer);
			if (_scaleContainer != null) Destroy(_scaleContainer);
			
			_axisHandles.Clear();
			_planeHandles.Clear();
			_axisMaterials.Clear();
			_planeMaterials.Clear();
			_rotationHandles.Clear();
			_rotationMaterials.Clear();
			_scaleHandles.Clear();
			_scaleMaterials.Clear();
		}
		
		void OnDestroy()
		{
			Cleanup();
		}
	}
}
