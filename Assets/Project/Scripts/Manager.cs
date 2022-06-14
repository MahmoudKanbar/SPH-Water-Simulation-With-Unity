using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;


public class Manager : MonoBehaviour
{
	[SerializeField] private ComputeShader computeShader;

	[Header("Environment")]
	public Vector3 gravity = new Vector3(0.0f, -10.0f, 0.0f);
	public Vector3Int generationVolume = new Vector3Int(32, 32, 32);

	public float TimeStep = 0.016f;
	public float Mass = 1.0f;
	public float radius = 0.5f;
	public float H = 2.0f;
	public float K = 1000.0f;
	public float Mu = 5.0f;
	public float Rho = 1.0f;
	public float Dampen = 0.5f;

	public float gridVolume = 50.0f;
	public float voxelVolume = 2.0f;

	public bool drawGrid = true;
	public bool drawGeneration = true;

	public Mesh mesh;
	public Material material;


	private Vector3[] particles;
	private ForceFieldContainer[] forceFields;
	private ExplosionFieldContainer[] explosionFields;
	private PlaneContainer[] planes;


	public static Manager Instance { get; private set; }
	public int particlesCount => generationVolume.x * generationVolume.y * generationVolume.z;
	public int voxelCapacity =>  (int)Mathf.Ceil(Mathf.Pow(voxelVolume / radius, 3));
	public int voxelsCount => (int)(gridVolume * gridVolume * gridVolume);
	public int flattenedVoxelsCapacity => voxelsCount * voxelCapacity;



	public float H2 => H * H;
	public float H4 => H2 * H2;
	public float H16 => H2 * H2 * H2 * H2;
	public float C => 4 * Mass / Mathf.PI / H16;
	public float C0 => Mass / Mathf.PI / H4;
	public float CP => 15 * K;
	public float CV => -40 * Mu;



	public int forceFieldsCount;
	public int explosionFieldsCount;
	public int planesCount;


	private Dictionary<string, ComputeBuffer> buffers = new Dictionary<string, ComputeBuffer>();
	private Dictionary<string, int> kernels = new Dictionary<string, int>();


	private void Awake()
	{
		if (Instance == null) Instance = this;
		else throw new System.Exception("There is already a Manager in the scene");

		drawGeneration = false;

		FindKernels();

		GenerateParticles();

		GetForceFields(0);
		GetExplosionFields(0);
		GetPlanes();

		InitiateBuffers();
		UpdateParameters();
	}


	private void GenerateParticles()
	{
		particles = new Vector3[particlesCount];
		int i = 0;
		for (int x = 0; x < generationVolume.x; x++)
		{
			for (int y = 0; y < generationVolume.y; y++)
			{
				for (int z = 0; z < generationVolume.z; z++)
				{
					var temp = new Vector3(generationVolume.x, generationVolume.y, generationVolume.z);
					particles[i] = new Vector3(x, y, z) + transform.position - temp / 2.0f;
					i++;
				}
			}
		}
	}

	private void GetForceFields(int use)
	{
		var temp = FindObjectsOfType<ForceField>();
		if (temp.Length == 0)
		{
			forceFields = new ForceFieldContainer[1];
			forceFieldsCount = 0;
			return;
		}

		forceFields = new ForceFieldContainer[temp.Length];
		forceFieldsCount = temp.Length;
		for (int i = 0; i < forceFieldsCount; i++)
		{
			if (temp[i].alwasOn) temp[i].container.use = 1;
			else temp[i].container.use = use;

			forceFields[i] = temp[i].container;
		}
	}

	private void GetExplosionFields(int use)
	{
		var temp = FindObjectsOfType<ExplosionField>();
		if (temp.Length == 0)
		{
			explosionFields = new ExplosionFieldContainer[1];
			explosionFieldsCount = 0;
			return;
		}

		explosionFields = new ExplosionFieldContainer[temp.Length];
		explosionFieldsCount = temp.Length;
		for (int i = 0; i < explosionFieldsCount; i++)
		{
			if (temp[i].alwasOn) temp[i].container.use = 1;
			else temp[i].container.use = use;

			explosionFields[i] = temp[i].container;
		}
	}

	private void GetPlanes()
	{
		var temp = FindObjectsOfType<Plane>();
		planes = new PlaneContainer[temp.Length + 6];
		planesCount = temp.Length  +6;
		var size = gridVolume * voxelVolume;

		// down
		planes[0] = new PlaneContainer()
		{
			position = new Vector3(size / 2, 0, size / 2),
			normal = new Vector3(0, 1, 0),
			x = new Vector3(1, 0, 0),
			y = new Vector3(0, 0, 1),
			size = Vector3.one * size * 2
		};

		// top
		planes[1] = new PlaneContainer()
		{
			position = new Vector3(size / 2, size, size / 2),
			normal = new Vector3(0, -1, 0),
			x = new Vector3(1, 0, 0),
			size = Vector3.one * size * 2
		};

		// right
		planes[2] = new PlaneContainer()
		{
			position = new Vector3(0, size / 2, size / 2),
			normal = new Vector3(1, 0, 0),
			x = new Vector3(0, 1, 0),
			y = new Vector3(0, 0, 1),
			size = Vector3.one * size * 2
		};

		// left
		planes[3] = new PlaneContainer()
		{
			position = new Vector3(size, size / 2, size / 2),
			normal = new Vector3(-1, 0, 0),
			x = new Vector3(0, 1, 0),
			y = new Vector3(0, 0, 1),
			size = Vector3.one * size * 2
		};

		// back
		planes[4] = new PlaneContainer()
		{
			position = new Vector3(size / 2, size / 2, 0),
			normal = new Vector3(0, 0, 1),
			x = new Vector3(1, 0, 0),
			y = new Vector3(0, 1, 0),
			size = Vector3.one * size * 2
		};

		// front
		planes[5] = new PlaneContainer()
		{
			position = new Vector3(size / 2, size / 2, size),
			normal = new Vector3(0, 0, -1),
			x = new Vector3(1, 0, 0),
			y = new Vector3(0, 1, 0),
			size = Vector3.one * size * 2
		};

		for (int i = 6; i < planesCount; i++)
		{
			planes[i] = temp[i - 6].container;
		}
	}

	private void FindKernels()
	{
		kernels.Add("ResetVoxelsPointers", computeShader.FindKernel("ResetVoxelsPointers"));
		kernels.Add("PopulateFlattenedVoxels", computeShader.FindKernel("PopulateFlattenedVoxels"));
		kernels.Add("ComputeDensity", computeShader.FindKernel("ComputeDensity"));
		kernels.Add("ComputeAcceleration", computeShader.FindKernel("ComputeAcceleration"));
		kernels.Add("ApplyExternalForces", computeShader.FindKernel("ApplyExternalForces"));
		kernels.Add("ApplyMotion", computeShader.FindKernel("ApplyMotion"));
		kernels.Add("ApplyResulotion", computeShader.FindKernel("ApplyResulotion"));
	}

	//int[] cornerNieghboursList;
	private void InitiateBuffers()
	{
		var velocities = new Vector3[particlesCount];
		var accelerations = new Vector3[particlesCount];
		var densities = new float[particlesCount];

		var voxelContainerList = new int[flattenedVoxelsCapacity];
		var voxelPointerList = new int[voxelsCount];
		//cornerNieghboursList = new int[voxelsCount * 64];

		var penetrations = new Vector3[particlesCount];
		var velocityReflections = new Vector3[particlesCount];
		var collisionCount = new int[particlesCount];

		buffers.Add("Particles", new ComputeBuffer(particlesCount, sizeof(float) * 3));
		buffers.Add("Velocities", new ComputeBuffer(particlesCount, sizeof(float) * 3));
		buffers.Add("Accelerations", new ComputeBuffer(particlesCount, sizeof(float) * 3));
		buffers.Add("Densities", new ComputeBuffer(particlesCount, sizeof(float)));

		buffers.Add("FlattenedVoxels", new ComputeBuffer(flattenedVoxelsCapacity, sizeof(int)));
		buffers.Add("VoxelsPointers", new ComputeBuffer(voxelsCount, sizeof(int)));

		buffers.Add("ForceFields", new ComputeBuffer(forceFields.Length, ForceFieldContainer.GetSize));
		buffers.Add("ExplosionFields", new ComputeBuffer(explosionFields.Length, ExplosionFieldContainer.GetSize));
		buffers.Add("Planes", new ComputeBuffer(planes.Length, PlaneContainer.GetSize));

		buffers.Add("Penetrations", new ComputeBuffer(particlesCount, sizeof(float) * 3));
		buffers.Add("VelocityReflections", new ComputeBuffer(particlesCount, sizeof(float) * 3));
		buffers.Add("CollisionCount", new ComputeBuffer(particlesCount, sizeof(int)));

		buffers["Particles"].SetData(particles);
		buffers["Velocities"].SetData(velocities);
		buffers["Accelerations"].SetData(accelerations);
		buffers["Densities"].SetData(densities);
		buffers["FlattenedVoxels"].SetData(voxelContainerList);
		buffers["VoxelsPointers"].SetData(voxelPointerList);

		buffers["ForceFields"].SetData(forceFields);
		buffers["ExplosionFields"].SetData(explosionFields);
		buffers["Planes"].SetData(planes);

		buffers["Penetrations"].SetData(penetrations);
		buffers["VelocityReflections"].SetData(velocityReflections);
		buffers["CollisionCount"].SetData(collisionCount);


		computeShader.SetBuffer(kernels["ResetVoxelsPointers"], "VoxelsPointers", buffers["VoxelsPointers"]);

		computeShader.SetBuffer(kernels["PopulateFlattenedVoxels"], "Particles", buffers["Particles"]);
		computeShader.SetBuffer(kernels["PopulateFlattenedVoxels"], "VoxelsPointers", buffers["VoxelsPointers"]);
		computeShader.SetBuffer(kernels["PopulateFlattenedVoxels"], "FlattenedVoxels", buffers["FlattenedVoxels"]);

		computeShader.SetBuffer(kernels["ComputeDensity"], "Particles", buffers["Particles"]);
		computeShader.SetBuffer(kernels["ComputeDensity"], "Densities", buffers["Densities"]);
		computeShader.SetBuffer(kernels["ComputeDensity"], "VoxelsPointers", buffers["VoxelsPointers"]);
		computeShader.SetBuffer(kernels["ComputeDensity"], "FlattenedVoxels", buffers["FlattenedVoxels"]);

		computeShader.SetBuffer(kernels["ComputeAcceleration"], "Particles", buffers["Particles"]);
		computeShader.SetBuffer(kernels["ComputeAcceleration"], "Velocities", buffers["Velocities"]);
		computeShader.SetBuffer(kernels["ComputeAcceleration"], "Accelerations", buffers["Accelerations"]);
		computeShader.SetBuffer(kernels["ComputeAcceleration"], "Densities", buffers["Densities"]);
		computeShader.SetBuffer(kernels["ComputeAcceleration"], "VoxelsPointers", buffers["VoxelsPointers"]);
		computeShader.SetBuffer(kernels["ComputeAcceleration"], "FlattenedVoxels", buffers["FlattenedVoxels"]);

		computeShader.SetBuffer(kernels["ApplyExternalForces"], "Particles", buffers["Particles"]);
		computeShader.SetBuffer(kernels["ApplyExternalForces"], "Accelerations", buffers["Accelerations"]);
		computeShader.SetBuffer(kernels["ApplyExternalForces"], "ForceFields", buffers["ForceFields"]);
		computeShader.SetBuffer(kernels["ApplyExternalForces"], "ExplosionFields", buffers["ExplosionFields"]);

		computeShader.SetBuffer(kernels["ApplyMotion"], "Particles", buffers["Particles"]);
		computeShader.SetBuffer(kernels["ApplyMotion"], "Velocities", buffers["Velocities"]);
		computeShader.SetBuffer(kernels["ApplyMotion"], "Accelerations", buffers["Accelerations"]);
		computeShader.SetBuffer(kernels["ApplyMotion"], "Planes", buffers["Planes"]);
		computeShader.SetBuffer(kernels["ApplyMotion"], "Penetrations", buffers["Penetrations"]);
		computeShader.SetBuffer(kernels["ApplyMotion"], "VelocityReflections", buffers["VelocityReflections"]);
		computeShader.SetBuffer(kernels["ApplyMotion"], "CollisionCount", buffers["CollisionCount"]);

		computeShader.SetBuffer(kernels["ApplyResulotion"], "Particles", buffers["Particles"]);
		computeShader.SetBuffer(kernels["ApplyResulotion"], "Velocities", buffers["Velocities"]);
		computeShader.SetBuffer(kernels["ApplyResulotion"], "Penetrations", buffers["Penetrations"]);
		computeShader.SetBuffer(kernels["ApplyResulotion"], "VelocityReflections", buffers["VelocityReflections"]);
		computeShader.SetBuffer(kernels["ApplyResulotion"], "CollisionCount", buffers["CollisionCount"]);

		uint[] args = { mesh.GetIndexCount(0), (uint)particlesCount, mesh.GetIndexStart(0), mesh.GetBaseVertex(0), 0 };
		buffers.Add("ArgsBuffer", new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments));
		buffers["ArgsBuffer"].SetData(args);
	}


	private void OnDrawGizmos()
	{
		if (drawGrid)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireCube(gridVolume * voxelVolume / 2.0f * Vector3.one, gridVolume * voxelVolume * Vector3.one);
		}

		if (drawGeneration)
		{
			Gizmos.color = Color.blue;
			Gizmos.DrawCube(transform.position, generationVolume);
		}
	}

	private void Update()
	{
		computeShader.Dispatch(kernels["ResetVoxelsPointers"], voxelsCount / 64 + 1, 1, 1);

		computeShader.Dispatch(kernels["PopulateFlattenedVoxels"], particlesCount / 64 + 1, 1, 1);

		computeShader.Dispatch(kernels["ComputeDensity"], particlesCount / 64 + 1, 1, 1);

		computeShader.Dispatch(kernels["ComputeAcceleration"], particlesCount / 64 + 1, 1, 1);

		computeShader.Dispatch(kernels["ApplyExternalForces"], particlesCount / 64 + 1, 1, 1);

		computeShader.Dispatch(kernels["ApplyMotion"], particlesCount / 64 + 1, 1, 1);

		computeShader.Dispatch(kernels["ApplyResulotion"], particlesCount / 64 + 1, 1, 1);

		material.SetBuffer("Particles", buffers["Particles"]);
		Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, new Vector3(500.0f, 500.0f, 500.0f)), buffers["ArgsBuffer"], castShadows: UnityEngine.Rendering.ShadowCastingMode.Off);
	}

	public void UpdateParameters()
	{
		var _gravity = new float[] { gravity.x, gravity.y, gravity.z };
		computeShader.SetFloats("Gravity", _gravity);

		computeShader.SetFloat("GridVolume", gridVolume);
		computeShader.SetFloat("VoxelVolume", voxelVolume);
		computeShader.SetFloat("CornerVolume", voxelVolume / 2);

		computeShader.SetInt("VoxelCapacity", voxelCapacity);
		computeShader.SetInt("ParticlesCount", particlesCount);
		computeShader.SetInt("VoxelsCount", voxelsCount);
		computeShader.SetInt("FlattenedVoxelsCapacity", flattenedVoxelsCapacity);
		computeShader.SetInt("ForceFieldsCount", forceFieldsCount);
		computeShader.SetInt("ExplosionFieldsCount", explosionFieldsCount);
		computeShader.SetInt("PlanesCount", planesCount);

		computeShader.SetFloat("H", H);
		computeShader.SetFloat("Radius", radius);
		computeShader.SetFloat("Mass", Mass);
		computeShader.SetFloat("PI", Mathf.PI);
		computeShader.SetFloat("K", K);
		computeShader.SetFloat("Mu", Mu);
		computeShader.SetFloat("Rho", Rho);
		computeShader.SetFloat("Dampen", Dampen);
		computeShader.SetFloat("TimeStep", TimeStep);

		computeShader.SetFloat("H2", H2);
		computeShader.SetFloat("H4", H4);
		computeShader.SetFloat("H16", H16);
		computeShader.SetFloat("C", C);
		computeShader.SetFloat("C0", C0);
		computeShader.SetFloat("CP", CP);
		computeShader.SetFloat("CV", CV);

		GetPlanes();
		buffers["Planes"].SetData(planes);
	}

	public void ActivatFields() => StartCoroutine(ActivateFieldCoroutine());
	private IEnumerator ActivateFieldCoroutine()
	{
		GetForceFields(1);
		GetExplosionFields(1);

		buffers["ForceFields"].SetData(forceFields);
		buffers["ExplosionFields"].SetData(explosionFields);
		
		yield return new WaitForSeconds(1.0f);

		GetForceFields(0);
		GetExplosionFields(0);

		buffers["ForceFields"].SetData(forceFields);
		buffers["ExplosionFields"].SetData(explosionFields);
	}

	private void OnDestroy()
	{
		foreach (var buffer in buffers.Values)
		{
			buffer.Release();
		}
	}
}
