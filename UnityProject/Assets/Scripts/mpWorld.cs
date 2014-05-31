using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
public struct mpParticle
{
    [FieldOffset( 0)] public Vector4 position;
    [FieldOffset(16)] public Vector4 velocity;
    [FieldOffset(32)] public float density;
    [FieldOffset(36)] public uint hash;
    [FieldOffset(40)] public int hit;
    [FieldOffset(44)] public float lifetime;
}

[StructLayout(LayoutKind.Explicit)]
struct mpKernelParams {
	[FieldOffset( 0)] public int SolverType;
	[FieldOffset( 4)] public float LifeTime;
	[FieldOffset( 8)] public float Timestep;
	[FieldOffset(12)] public float Decelerate;
	[FieldOffset(16)] public float SPHPressureStiffness;
	[FieldOffset(20)] public float SPHRestDensity;
	[FieldOffset(24)] public float SPHParticleMass;
	[FieldOffset(28)] public float SPHViscosity;
	[FieldOffset(32)] public float ImpPressureStiffness;
};

public enum mpSolverType
{
	Impulse = 0,
	SPH = 1,
	SPHEstimate = 2,
}

public class mpWorld : MonoBehaviour {

	[DllImport ("MassParticle")] private static extern void mpSetViewProjectionMatrix(Matrix4x4 view, Matrix4x4 proj);
	[DllImport ("MassParticle")] private static extern void mpSetRenderTargets(IntPtr renderTexture, IntPtr depthTexture);
	[DllImport ("MassParticle")] private static extern void mpClearParticles();
	
	[DllImport ("MassParticle")] private static extern mpKernelParams mpGetKernelParams();
	[DllImport ("MassParticle")] private static extern void mpSetKernelParams(ref mpKernelParams p);
	
	
	[DllImport ("MassParticle")] private static extern uint mpGetNumParticles();
	[DllImport ("MassParticle")] unsafe private static extern mpParticle* mpGetParticles();

	[DllImport ("MassParticle")] private static extern uint mpAddBoxCollider(int owner, Matrix4x4 transform, Vector3 size);
	[DllImport ("MassParticle")] private static extern uint mpAddSphereCollider(int owner, Vector3 center, float radius);

	[DllImport ("MassParticle")] private static extern void mpUpdate (float dt);


	public float particleRadius;
	public float particleLifeTime;
	public float timeStep;
	public float deceleration;
	public mpSolverType solverType;

	private Collider[] colliders;


	mpWorld()
	{
		mpKernelParams p = mpGetKernelParams();
		particleLifeTime = p.LifeTime;
		timeStep = p.Timestep;
		deceleration = p.Decelerate;
		solverType = (mpSolverType)p.SolverType;
	}

	void Start () {
        mpClearParticles();
	}

	void Update()
	{
		{
			mpKernelParams p = mpGetKernelParams();
			p.LifeTime = particleLifeTime;
			p.Timestep = timeStep;
			p.Decelerate = deceleration;
			p.SolverType = (int)solverType;
			mpSetKernelParams(ref p);
		}
		{
			colliders = Physics.OverlapSphere( transform.position, 10.0f );
			for (int i = 0; i < colliders.Length; ++i )
			{
				Collider col = colliders[i];
				SphereCollider sphere = col as SphereCollider;
				BoxCollider box = col as BoxCollider;
				int ownerid = col.rigidbody ? i : -1;
				if(sphere) {
					mpAddSphereCollider(ownerid, sphere.transform.position, sphere.radius);
				}
				else if (box)
				{
					mpAddBoxCollider(ownerid, box.transform.localToWorldMatrix, box.size);
				}
			}
		}

		mpUpdate (Time.timeSinceLevelLoad);
		ProcessParticleCollision();
	}
	
	void OnRenderObject()
	{		
		UnityEngine.Camera cam = UnityEngine.Camera.current;
		if (cam) {
			mpSetViewProjectionMatrix(cam.worldToCameraMatrix, cam.projectionMatrix);
		}
		GL.IssuePluginEvent (1);
	}


	unsafe void ProcessParticleCollision()
	{
		uint numParticles = mpGetNumParticles();
		mpParticle *particles = mpGetParticles();
		for(uint i=0; i<numParticles; ++i) {
			if(particles[i].hit != -1) {
				Collider col = colliders[particles[i].hit];
				if(col.rigidbody) {
					Vector3 vel = *(Vector3*)&particles[i].velocity;
					col.rigidbody.AddForceAtPosition( vel * 0.1f, *(Vector3*)&particles[i].position );
				}
			}
		}
	}
}
