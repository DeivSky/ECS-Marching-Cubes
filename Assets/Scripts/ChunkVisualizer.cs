using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace MarchingCubes
{
	public class ChunkVisualizer : MonoBehaviour
	{
		public bool drawGizmosBounds;
		public Color boundsColor = Color.green;

		public bool drawGizmosMesh;
		public Color meshColor = Color.magenta;

		[Header("Warning!")]
		public bool showCoords;
		private bool _showCoords;

		private EntityManager em;
		private MarchingCubesData data;
		private RectTransform[] transforms;
		private TextMeshPro[] texts;

		private Dictionary<Entity, MarchingChunk> chunks;
		private Dictionary<Entity, RenderMesh> renderers;
		private bool isInit;

		private void Start()
		{
			em = World.DefaultGameObjectInjectionWorld.EntityManager;

			var q = em.CreateEntityQuery(ComponentType.ReadOnly<MarchingCubesData>());
			var datas = q.ToComponentDataArray<MarchingCubesData>(Allocator.Temp);
			data = datas[0];
			datas.Dispose();

			chunks = new Dictionary<Entity, MarchingChunk>(256);
			renderers = new Dictionary<Entity, RenderMesh>(256);
		}

		private void Init()
		{
			if (isInit)
				return;

			var entities = em.GetAllEntities(Allocator.Temp);
			for (int i = 0; i < entities.Length; i++)
			{
				if (!em.HasComponent<MarchingChunk>(entities[i]))
					continue;

				chunks.Add(entities[i], em.GetComponentData<MarchingChunk>(entities[i]));
				renderers.Add(entities[i], em.GetSharedComponentData<RenderMesh>(entities[i]));
			}
			entities.Dispose();
			if (chunks.Count == 0)
				return;

			

			isInit = true;
		}

		void Update()
		{
			if (!showCoords && !drawGizmosBounds && !drawGizmosMesh)
				return;

			Init();

			if (!isInit)
				return;

			UpdateEntityData();

			if (_showCoords != showCoords)
				ToggleShowCoords();

			if (showCoords)
				UpdateCoords();
		}

		private void OnDrawGizmos()
		{
			if (drawGizmosBounds)
				DrawMeshBounds();

			if (drawGizmosMesh)
				DrawGizmosMesh();
		}

		private void UpdateEntityData()
		{
			var keys = new Entity[chunks.Count];
			chunks.Keys.CopyTo(keys, 0);
			for (int i = 0; i < keys.Length; i++)
				chunks[keys[i]] = em.GetComponentData<MarchingChunk>(keys[i]);			
		}

		private void UpdateCoords()
		{
			int i = 0;
			foreach (var entChunk in chunks)
			{
				if (em.GetEnabled(entChunk.Key))
				{
					transforms[i].position = (float3)entChunk.Value.Coordinate * data.Terrain.BoundsSize + new float3(0, data.Terrain.BoundsSize / 2f, 0);
					transforms[i].name = entChunk.Key.ToString() + " " + entChunk.Value.ToString();
					texts[i].text = entChunk.Value.ToString();
				}
				else
				{
					texts[i].text = string.Empty;
					transforms[i].name = entChunk.Key.ToString() + " (disabled)";
				}
				i++;
			}
		}

		private void ToggleShowCoords()
		{
			if (!_showCoords)
			{
				int count = chunks.Count;
				transforms = new RectTransform[count];
				texts = new TextMeshPro[count];
				for (int i = 0; i < count; i++)
				{
					var go = new GameObject();
					texts[i] = go.AddComponent<TextMeshPro>();
					transforms[i] = go.GetComponent<RectTransform>();
					transforms[i].SetParent(transform);
					texts[i].fontSize = 8;
					texts[i].alignment = TextAlignmentOptions.Center;
				}

				_showCoords = true;
			}
			else
			{
				for (int i = 0; i < transforms.Length; i++)
					Destroy(transforms[i].gameObject);

				texts = null;
				transforms = null;
				_showCoords = false;
			}
		}

		private void DrawMeshBounds()
		{
			if (!Application.isPlaying)
				return;

			Gizmos.color = boundsColor;
			foreach (var entChunk in chunks)
				if (em.GetEnabled(entChunk.Key))
					Gizmos.DrawWireCube((float3)entChunk.Value.Coordinate * data.Terrain.BoundsSize, renderers[entChunk.Key].mesh.bounds.size);
		}

		private void DrawGizmosMesh()
		{
			if (!Application.isPlaying)
				return;

			Gizmos.color = meshColor;
			foreach (var entRender in renderers)
				if (em.GetEnabled(entRender.Key))
					if (entRender.Value.mesh.normals.Length > 0)
						Gizmos.DrawWireMesh(entRender.Value.mesh);
		}
	}
}
