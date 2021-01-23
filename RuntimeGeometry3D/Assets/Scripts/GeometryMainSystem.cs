using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Tiny;
using Unity.Tiny.Input;
using Unity.Tiny.Rendering;
using Unity.Tiny.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace RuntimeGeometryExample
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(RenderGraphBuilder))]
    public class GeometryTestMain : SystemBase
    {
        Entity CreateMeshEntity(float innerR, int innerN, float outerR, int outerN, int p, int q, float innerE, float outerE)
        {
            Entity e = EntityManager.CreateEntity();
            MeshBounds bounds;
            LitMeshRenderData lmrd;
            MeshHelper.CreateSuperTorusKnotMesh(innerR, innerN, outerR, outerN, p, q, innerE, outerE, out bounds, out lmrd);
            EntityManager.AddComponentData(e, lmrd);
            EntityManager.AddComponentData(e, bounds);
            return e;
        }

        Entity CreateCamera(float aspect, float4 background)
        {
            var ecam = EntityManager.CreateEntity(typeof(LocalToWorld), typeof(Translation), typeof(Rotation));
            var cam = new Camera();
            cam.clipZNear = 0.1f;
            cam.clipZFar = 50.0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(background.x, background.y, background.z, background.w);
            cam.viewportRect = new Rect(0, 0, 1, 1);
            cam.aspect = aspect;
            cam.fov = 60;
            cam.mode = ProjectionMode.Perspective;
            EntityManager.AddComponentData(ecam, cam);
            EntityManager.SetComponentData(ecam, new Translation { Value = new float3(0, 0, -4.0f) });
            EntityManager.SetComponentData(ecam, new Rotation { Value = quaternion.identity });
            return ecam;
        }

        Entity CreateLitRenderer(Entity eMesh, Entity eMaterial, quaternion rot, float3 pos, float3 scale)
        {
            Entity erendLit = EntityManager.CreateEntity();

            var lmrd = EntityManager.GetComponentData<LitMeshRenderData>(eMesh);
            int indexCount = lmrd.Mesh.Value.Indices.Length;

            EntityManager.AddComponentData(erendLit, new MeshRenderer   // renderer -> maps to shader to use
            {
                material = eMaterial,
                mesh = eMesh,
                startIndex = 0,
                indexCount = indexCount
            });
            EntityManager.AddComponentData(erendLit, new LitMeshRenderer());
            EntityManager.AddComponentData(erendLit, new LocalToWorld
            {
                Value = float4x4.identity
            });
            EntityManager.AddComponentData(erendLit, new Translation
            {
                Value = pos
            });
            EntityManager.AddComponentData(erendLit, new Rotation
            {
                Value = rot
            });
            if (scale.x != scale.y || scale.y != scale.z)
            {
                EntityManager.AddComponentData(erendLit, new NonUniformScale
                {
                    Value = scale
                });
            }
            else if (scale.x != 1.0f)
            {
                EntityManager.AddComponentData(erendLit, new Scale
                {
                    Value = scale.x
                });
            }
            EntityManager.AddComponentData(erendLit, new WorldBounds());
            return erendLit;
        }

        bool created;

        float3 drawColor = new float3(1);
        int drawColorIndex = 0;
        static readonly float3[] colorPalette = new float3[]
        {
            new float3(1, 1, 1),
            new float3(1, .2f, .2f),
            new float3(.2f, 1, .2f),
            new float3(.2f, .2f, 1),
            new float3(1, 1, .2f),
            new float3(.2f, 1, 1),
            new float3(1, .2f, 1)
        };

        float drawSize = .05f;
        int drawSizeIndex = 1;
        static readonly float[] sizePalette = new float[]
        {
            .025f,
            .05f,
            .1f,
            .2f,
            .4f
        };

        int materialIndex = 0;

        Entity eCam;
        Entity ePlainMaterial;
        Entity eMetalMaterial;
        Entity eFirstKnot;
        Entity eMeshDonut;

        Entity eCurrentShape;
        Entity eCurrentMaterial;
        NativeList<float3> drawList;
        NativeList<Entity> strokeStack;
        NativeList<Entity> cameraList;

        Unity.Mathematics.Random random;

        ScreenToWorld s2w;

        public void CreateScene()
        {
            // one startup main camera
            eCam = CreateCamera(1920.0f / 1080.0f, new float4(1.2f, 1.2f, 1.2f, 1));

            // create meshes
            eMeshDonut = CreateMeshEntity(.1f, 64, .65f, 128, 3, 2, .9f, .8f);

            // create materials
            eMetalMaterial = EntityManager.CreateEntity();
            EntityManager.AddComponentData(eMetalMaterial, new LitMaterial
            {
                texAlbedoOpacity = Entity.Null,
                texMetal = Entity.Null,
                texNormal = Entity.Null,
                texEmissive = Entity.Null,
                constEmissive = new float3(0),
                constOpacity = 1.0f,
                constAlbedo = new float3(1),
                constMetal = 1.0f,
                constSmoothness = .68f,
                normalMapZScale = 1.0f,
                twoSided = false,
                transparent = false,
                scale = new float2(1, 1),
                offset = new float2(0, 0)
            });

            ePlainMaterial = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePlainMaterial, new LitMaterial
            {
                texAlbedoOpacity = Entity.Null,
                texMetal = Entity.Null,
                texNormal = Entity.Null,
                texEmissive = Entity.Null,
                constEmissive = new float3(0),
                constOpacity = 1.0f,
                constAlbedo = new float3(1),
                constMetal = 0.0f,
                constSmoothness = .18f,
                normalMapZScale = 1.0f,
                twoSided = false,
                transparent = false,
                scale = new float2(1, 1),
                offset = new float2(0, 0)
            });

            eCurrentMaterial = ePlainMaterial;

            // lights
            float3[] lightcolor = new float3[] { new float3(1.0f, .3f, .2f), new float3(.1f, 1.0f, .2f), new float3(.1f, .2f, 1.0f) };
            float3[] lightdir = new float3[] { new float3(-1, -1, 1), new float3(1, 1, 1), new float3(0, 1, 1) };
            Assert.IsTrue(lightdir.Length == lightcolor.Length);
            for (int i = 0; i < lightcolor.Length; i++)
            {
                Entity eDirLight = EntityManager.CreateEntity();
                EntityManager.AddComponentData(eDirLight, new Light
                {
                    intensity = .5f,
                    color = lightcolor[i]
                });
                EntityManager.AddComponentData(eDirLight, new DirectionalLight {});
                EntityManager.AddComponentData(eDirLight, new LocalToWorld {});
                EntityManager.AddComponentData(eDirLight, new NonUniformScale { Value = new float3(1) });
                EntityManager.AddComponentData(eDirLight, new Rotation
                {
                    Value = quaternion.LookRotationSafe(lightdir[i], new float3(1, 0, 0))
                });
            }

            // renderer
            eFirstKnot = CreateLitRenderer(eMeshDonut, ePlainMaterial, quaternion.identity, new float3(0), new float3(1));
            EntityManager.AddComponentData(eFirstKnot, new DemoSpinner { spin = math.normalize(new quaternion(0, .2f, .1f, 1)) });
            strokeStack.Add(eFirstKnot);
        }

        protected override void OnCreate()
        {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            // World.GetExistingSystem<KeyControlsSystem>().m_configAlwaysRun = true;
            drawList = new NativeList<float3>(Allocator.Persistent);
            strokeStack = new NativeList<Entity>(Allocator.Persistent);
            cameraList = new NativeList<Entity>(Allocator.Persistent);
            random = new Unity.Mathematics.Random(23);
        }

        protected override void OnDestroy()
        {
            cameraList.Dispose();
            drawList.Dispose();
            strokeStack.Dispose();
        }

        void BeginStroke(float2 inputPos)
        {
            if (eCurrentShape != Entity.Null)
                EndStroke();
            float3 pos = s2w.InputPosToWorldSpacePos(inputPos, 4.0f);

            // start a new shape
            eCurrentShape = EntityManager.CreateEntity();
            EntityManager.AddBuffer<DynamicLitVertex>(eCurrentShape);
            EntityManager.AddBuffer<DynamicIndex>(eCurrentShape);
            var iBuffer = EntityManager.GetBuffer<DynamicIndex>(eCurrentShape);
            var vBuffer = EntityManager.GetBuffer<DynamicLitVertex>(eCurrentShape);
            int tess = 65;
            vBuffer.Capacity = 0x10000;
            vBuffer.ResizeUninitialized(tess * tess);
            iBuffer.Capacity = 0x60000;
            iBuffer.ResizeUninitialized((tess - 1) * (tess - 1) * 6);
            DynamicMeshData dmd = new DynamicMeshData
            {
                Dirty = true,
                IndexCapacity = iBuffer.Capacity,
                VertexCapacity = vBuffer.Capacity,
                NumIndices = iBuffer.Length,
                NumVertices = vBuffer.Length,
                UseDynamicGPUBuffer = true
            };
            MeshBounds mb;
            MeshHelper.CreateSuperEllipsoid(new float3(drawSize * 2.0f),
                vBuffer.AsNativeArray().Reinterpret<DynamicLitVertex, LitVertex>(),
                iBuffer.AsNativeArray().Reinterpret<DynamicIndex, ushort>(),
                random.NextFloat(0.04f, 3.0f), random.NextFloat(0.04f, 3.0f), tess, tess,
                out mb.Bounds); // start with a dot.. box
            MeshHelper.SetAlbedoColor(
                vBuffer.AsNativeArray().Reinterpret<DynamicLitVertex, LitVertex>(),
                new float4(drawColor, 1));
            EntityManager.AddComponentData<DynamicMeshData>(eCurrentShape, dmd);
            EntityManager.AddComponentData<MeshRenderer>(eCurrentShape, new MeshRenderer
            {
                mesh = eCurrentShape,
                material = eCurrentMaterial,
                startIndex = 0,
                indexCount = dmd.NumIndices // because mesh renderers can render only parts of a mesh (sub-mesh) we need to also update the count here
            });
            EntityManager.AddComponentData(eCurrentShape, new LitMeshRenderer());
            EntityManager.AddComponentData(eCurrentShape, mb);
            EntityManager.AddComponentData<Translation>(eCurrentShape, new Translation { Value = pos });
            EntityManager.AddComponentData<Rotation>(eCurrentShape, new Rotation { Value = quaternion.identity });
            EntityManager.AddComponentData<LocalToWorld>(eCurrentShape, new LocalToWorld { Value = float4x4.Translate(pos) });
            EntityManager.AddComponentData(eCurrentShape, new DemoSpinner { spin =  math.normalize(new quaternion(new float4(random.NextFloat3Direction(), 1))) });
            //EntityManager.AddComponentData(eCurrentShape, new GizmoNormalsAndTangents { width = 2.0f, length = .1f });
            //EntityManager.AddComponentData(eCurrentShape, new GizmoObjectBoundingBox {  color = new float4(0,0,0,1), width=4.0f });
            //EntityManager.AddComponentData(eCurrentShape, new GizmoBoundingSphere {  subdiv=32, width=4.0f });

            // start shaper
            drawList.Clear();
            drawList.Add(pos);
        }

        void ContinueStroke(float2 inputPos)
        {
            if (eCurrentShape == Entity.Null)
                return;
            float3 pos = s2w.InputPosToWorldSpacePos(inputPos, 4.0f);
            drawList.Add(pos);
            var resampleList = MeshHelper.ResampleCatmullRom(drawList, .075f, false, Allocator.TempJob);
            if (resampleList.Length >= 3)
            {
                // replace the existing CPU mesh, invalidate content
                var iBuffer = EntityManager.GetBuffer<DynamicIndex>(eCurrentShape);
                var vBuffer = EntityManager.GetBuffer<DynamicLitVertex>(eCurrentShape);
                const int nSegments = 9;
                int ni = MeshHelper.ExtrudedLineMeshRequiredIndices(resampleList.Length, nSegments);
                int nv = MeshHelper.ExtrudedLineMeshRequiredVertices(resampleList.Length, nSegments);
                if (ni <= iBuffer.Capacity && nv <= vBuffer.Capacity)
                {
                    vBuffer.ResizeUninitialized(nv);
                    iBuffer.ResizeUninitialized(ni);
                    MeshHelper.SmoothCurve(resampleList);
                    MeshHelper.SmoothCurve(resampleList);
                    MeshHelper.FillExtrudedLineCircle(
                        vBuffer.AsNativeArray().Reinterpret<DynamicLitVertex, LitVertex>(),
                        iBuffer.AsNativeArray().Reinterpret<DynamicIndex, ushort>(),
                        drawSize, drawSize, .5f, resampleList, nSegments, false);
                    MeshHelper.SetAlbedoColor(
                        vBuffer.AsNativeArray().Reinterpret<DynamicLitVertex, LitVertex>(),
                        new float4(drawColor, 1));
                    DynamicMeshData dmd = EntityManager.GetComponentData<DynamicMeshData>(eCurrentShape);
                    dmd.Dirty = true;
                    dmd.NumIndices = iBuffer.Length;
                    dmd.NumVertices = vBuffer.Length;
                    MeshBounds mb;
                    mb.Bounds = MeshHelper.ComputeBounds(vBuffer.AsNativeArray().Reinterpret<DynamicLitVertex, LitVertex>());
                    EntityManager.SetComponentData<DynamicMeshData>(eCurrentShape, dmd);
                    EntityManager.SetComponentData(eCurrentShape, mb);
                    EntityManager.SetComponentData<Translation>(eCurrentShape, new Translation { Value = new float3(0) });
                    EntityManager.SetComponentData<Rotation>(eCurrentShape, new Rotation { Value = quaternion.identity });
                    EntityManager.SetComponentData<LocalToWorld>(eCurrentShape, new LocalToWorld { Value = float4x4.identity });
                    // have to update the index count of the submesh
                    var mr = EntityManager.GetComponentData<MeshRenderer>(eCurrentShape);
                    mr.indexCount = dmd.NumIndices;
                    mr.material = eCurrentMaterial;
                    EntityManager.SetComponentData(eCurrentShape, mr);
                    if (EntityManager.HasComponent<DemoSpinner>(eCurrentShape))
                        EntityManager.RemoveComponent<DemoSpinner>(eCurrentShape);
                }
                else
                {
                    Debug.Log("Stroke is too long.");
                }
            }
            resampleList.Dispose();
        }

        void EndStroke()
        {
            if (eCurrentShape != Entity.Null)
            {
                DynamicMeshData dmd = EntityManager.GetComponentData<DynamicMeshData>(eCurrentShape);
                dmd.Dirty = true;
                dmd.UseDynamicGPUBuffer = false;
                EntityManager.SetComponentData(eCurrentShape, dmd);
                //EntityManager.RemoveComponent<GizmoNormalsAndTangents>(eCurrentShape);
                //EntityManager.RemoveComponent<GizmoObjectBoundingBox>(eCurrentShape);
                strokeStack.Add(eCurrentShape);
            }
            eCurrentShape = Entity.Null;
            drawList.Clear();
        }

        protected override void OnUpdate()
        {
            if (!created)
            {
                CreateScene();
                s2w = World.GetExistingSystem<ScreenToWorld>();
                created = true;
            }

            // remove ambient lights. they get converted from multiple scenes right now
            Entities.WithAll<AmbientLight>().ForEach((Entity e, ref Light l) => {
                l.intensity = 0;
            }).Run();

            var di = GetSingleton<DisplayInfo>();
            float dt = World.Time.DeltaTime;
            var input = World.GetExistingSystem<InputSystem>();

            if (input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift))
                return;

            // new camera from current camera
            if (input.GetKeyDown(KeyCode.W))
            {
                if (cameraList.Length < 32)
                {
                    Entity eNewCam = EntityManager.Instantiate(eCam);
                    var cam = EntityManager.GetComponentData<Camera>(eNewCam);
                    cam.viewportRect.x = (cameraList.Length % 8) / 9.0f;
                    cam.viewportRect.width = 1.0f / 10.0f;
                    cam.viewportRect.y = (cameraList.Length / 8) / 9.0f;
                    cam.viewportRect.height = 1.0f / 10.0f;
                    cam.backgroundColor.r = random.NextFloat();
                    cam.backgroundColor.g = random.NextFloat();
                    cam.backgroundColor.b = random.NextFloat();
                    EntityManager.SetComponentData<Camera>(eNewCam, cam);
                    cameraList.Add(eNewCam);
                }
            }

            // C - Color
            if (input.GetKeyDown(KeyCode.C))
            {
                drawColorIndex++;
                if (drawColorIndex == colorPalette.Length)
                    drawColorIndex = 0;
                drawColor = colorPalette[drawColorIndex];
            }

            // M - Material
            if (input.GetKeyDown(KeyCode.M))
            {
                materialIndex++;
                switch (materialIndex)
                {
                    case 2:
                    case 0:
                        eCurrentMaterial = ePlainMaterial;
                        materialIndex = 0;
                        break;
                    case 1:
                        eCurrentMaterial = eMetalMaterial;
                        break;
                }
            }

            // S - Size
            if (input.GetKeyDown(KeyCode.S))
            {
                drawSizeIndex++;
                if (drawSizeIndex == sizePalette.Length)
                    drawSizeIndex = 0;
                drawSize = sizePalette[drawSizeIndex];
            }

            // Z - Delete last shape
            if (input.GetKeyDown(KeyCode.Z))
            {
                EndStroke();
                if (strokeStack.Length > 0)
                {
                    EntityManager.DestroyEntity(strokeStack[strokeStack.Length - 1]);
                    strokeStack.RemoveAtSwapBack(strokeStack.Length - 1);
                }
            }

            // click to start drawing
            if (input.IsMousePresent())
            {
                if (input.GetMouseButtonDown(0))
                    BeginStroke(input.GetInputPosition());
                else if (input.GetMouseButton(0))
                    ContinueStroke(input.GetInputPosition());
                else if (input.GetMouseButtonUp(0))
                    EndStroke();
            }
            else if (input.IsTouchSupported())
            {
                if (input.TouchCount() == 1)
                {
                    Touch t0 = input.GetTouch(0);
                    switch (t0.phase)
                    {
                        case TouchState.Began:
                            BeginStroke(new float2(t0.x, t0.y));
                            break;
                        case TouchState.Moved:
                            ContinueStroke(new float2(t0.x, t0.y));
                            break;
                        case TouchState.Canceled:
                        case TouchState.Ended:
                            EndStroke();
                            break;
                    }
                }
            }
        }
    }
}
