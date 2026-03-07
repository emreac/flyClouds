#ifndef URP_VOLUMETRIC_CLOUDS_HLSL
#define URP_VOLUMETRIC_CLOUDS_HLSL

#include "./VolumetricCloudsDefs.hlsl"
#include "./VolumetricCloudsUtilities.hlsl"

CBUFFER_START(CloudCurvature)
float _WorldCurvature;     // e.g. 0.00001
float _CurvatureOffsetY;   // usually 0
CBUFFER_END

float3 ApplyWorldCurvature(float3 positionWS)
{
    float2 xz = positionWS.xz;
    float bend = dot(xz, xz) * _WorldCurvature;

    positionWS.y -= bend;
    positionWS.y += _CurvatureOffsetY;

    return positionWS;
}

CloudRay BuildCloudsRay(float2 screenUV, float depth, half3 invViewDirWS, bool isOccluded)
{
    CloudRay ray;

#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    ray.originWS = GetCameraPositionWS();
#else
    // In torus mode we MUST use the real camera position
    // because the tube is a finite object in world space
    ray.originWS = (_TorusMajorRadius > 0.5)
        ? GetCameraPositionWS()
        : float3(0.0, 0.0, 0.0);
#endif

    ray.direction = invViewDirWS;

    // Compute the max cloud ray length
    // For opaque objects, we only care about clouds in front of them.
#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    // The depth may from a high-res texture which isn't ideal but can save performance.
    float distance = LinearEyeDepth(depth, _ZBufferParams) * rcp(dot(ray.direction, -UNITY_MATRIX_V[2].xyz));
    ray.maxRayLength = lerp(MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE, distance, isOccluded);
#else
    ray.maxRayLength = MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE;
#endif

    ray.integrationNoise = GenerateRandomFloat(screenUV);

    return ray;
}

VolumetricRayResult TraceVolumetricRay(CloudRay cloudRay)
{
    // Initiliaze the volumetric ray
    VolumetricRayResult volumetricRay;
    volumetricRay.scattering = 0.0;
    volumetricRay.ambient = 0.0;
    volumetricRay.transmittance = 1.0;
    volumetricRay.meanDistance = FLT_MAX;
    volumetricRay.invalidRay = true;

    // Determine if ray intersects bounding volume, if the ray does not intersect the cloud volume AABB, skip right away
    RayMarchRange rayMarchRange;
    if (GetCloudVolumeIntersection(cloudRay.originWS, cloudRay.direction, rayMarchRange))
    {
        if (cloudRay.maxRayLength >= rayMarchRange.start)
        {
            // Initialize the depth for accumulation
            volumetricRay.meanDistance = 0.0;

            // Total distance that the ray must travel including empty spaces
            // Clamp the travel distance to whatever is closer
            // - Sky Occluder
            // - Volume end
            // - Far plane
            float totalDistance = min(rayMarchRange.end, cloudRay.maxRayLength) - rayMarchRange.start;

            // Evaluate our integration step
            float stepS = min(totalDistance / (float)_NumPrimarySteps, _MaxStepSize);
            totalDistance = stepS * _NumPrimarySteps;

            // Compute the environment lighting that is going to be used for the cloud evaluation
            float3 rayMarchStartPS = ConvertToPS(cloudRay.originWS) + rayMarchRange.start * cloudRay.direction;
            float3 rayMarchEndPS = rayMarchStartPS + totalDistance * cloudRay.direction;

            // Tracking the number of steps that have been made
            int currentIndex = 0;

            // Normalization value of the depth
            float meanDistanceDivider = 0.0;

            // Current position for the evaluation, apply blue noise to start position
            float currentDistance = cloudRay.integrationNoise;
            float3 currentPositionWS = cloudRay.originWS + (rayMarchRange.start + currentDistance) * cloudRay.direction;

            // Initialize the values for the optimized ray marching
            bool activeSampling = true;
            int sequentialEmptySamples = 0;

            // Do the ray march for every step that we can.
            while (currentIndex < (int)_NumPrimarySteps && currentDistance < totalDistance)
            {
                // Compute the camera-distance based attenuation
                float densityAttenuationValue = DensityFadeValue(rayMarchRange.start + currentDistance);
                // Compute the mip offset for the erosion texture
                float erosionMipOffset = ErosionMipOffset(rayMarchRange.start + currentDistance);

                // Accumulate in WS and convert at each iteration to avoid precision issues
                float3 currentPositionPS;
                if (_TorusMajorRadius > 0.5)
                {
                    // TORUS MODE: remap torus tube coordinates into cloud altitude space
                    // relPos is position relative to torus center
                    float3 relPos = currentPositionWS - _TorusCenter;
                    currentPositionPS = TorusToSpherePS(relPos);
                }
                else
                {
                    // NORMAL MODE: original world curvature
                    float3 curvedPositionWS = ApplyWorldCurvature(currentPositionWS);
                    currentPositionPS = ConvertToPS(curvedPositionWS);
                }

                // Should we be evaluating the clouds or just doing the large ray marching
                if (activeSampling)
                {
                    // If the density is null, we can skip as there will be no contribution
                    CloudProperties properties;
                    EvaluateCloudProperties(currentPositionPS, 0.0, erosionMipOffset, false, false, properties);

                    // Apply the fade in function to the density
                    properties.density *= densityAttenuationValue;

                    if (properties.density > CLOUD_DENSITY_TRESHOLD)
                    {
                        // Contribute to the average depth (must be done first in case we end up inside a cloud at the next step)
                        half transmitanceXdensity = volumetricRay.transmittance * properties.density;
                        volumetricRay.meanDistance += (rayMarchRange.start + currentDistance) * transmitanceXdensity;
                        meanDistanceDivider += transmitanceXdensity;

                        // Evaluate the cloud at the position
                        EvaluateCloud(properties, cloudRay.direction, currentPositionPS, stepS, currentDistance / totalDistance, volumetricRay);

                        // if most of the energy is absorbed, just leave.
                        if (volumetricRay.transmittance < 0.003)
                        {
                            volumetricRay.transmittance = 0.0;
                            break;
                        }

                        // Reset the empty sample counter
                        sequentialEmptySamples = 0;
                    }
                    else
                        sequentialEmptySamples++;

                    // If it has been more than EMPTY_STEPS_BEFORE_LARGE_STEPS, disable active sampling and start large steps
                    if (sequentialEmptySamples == EMPTY_STEPS_BEFORE_LARGE_STEPS)
                        activeSampling = false;

                    // Do the next step
                    float relativeStepSize = lerp(cloudRay.integrationNoise, 1.0, saturate(currentIndex));
                    currentPositionWS += cloudRay.direction * stepS * relativeStepSize;
                    currentDistance += stepS * relativeStepSize;

                }
                else
                {
                    CloudProperties properties;
                    EvaluateCloudProperties(currentPositionPS, 1.0, 0.0, true, false, properties);

                    // Apply the fade in function to the density
                    properties.density *= densityAttenuationValue;

                    // If the density is lower than our tolerance,
                    if (properties.density < CLOUD_DENSITY_TRESHOLD)
                    {
                        currentPositionWS += cloudRay.direction * stepS * 2.0;
                        currentDistance += stepS * 2.0;
                    }
                    else
                    {
                        // Somewhere between this step and the previous clouds started
                        // We reset all the counters and enable active sampling
                        currentPositionWS -= cloudRay.direction * stepS;
                        currentDistance -= stepS;
                        currentIndex -= 1;
                        activeSampling = true;
                        sequentialEmptySamples = 0;
                    }
                }
                currentIndex++;
            }

            // Normalized the depth we computed
            if (volumetricRay.meanDistance != 0.0)
            {
                volumetricRay.invalidRay = false;
                volumetricRay.meanDistance /= meanDistanceDivider;
                volumetricRay.meanDistance = min(volumetricRay.meanDistance, cloudRay.maxRayLength);

                float3 meanPosWS = cloudRay.originWS + volumetricRay.meanDistance * cloudRay.direction;
                float3 currentPositionPS = (_TorusMajorRadius > 0.5)
                    ? TorusToSpherePS(meanPosWS - _TorusCenter)
                    : ConvertToPS(meanPosWS);
                float relativeHeight = EvaluateNormalizedCloudHeight(currentPositionPS);

                Light sun = GetMainLight();

                // Evaluate the sun color at the position
            #ifdef _PHYSICALLY_BASED_SUN
                half3 sunColor = _SunColor * EvaluateSunColorAttenuation(currentPositionPS, sun.direction, true) * _SunLightDimmer; // _SunColor includes PI
            #else
                half3 sunColor = sun.color * PI * _SunLightDimmer;
            #endif

                // Evaluate the environement lighting contribution
            #ifdef _CLOUDS_AMBIENT_PROBE
                half3 ambientTermTop = SAMPLE_TEXTURECUBE_LOD(_VolumetricCloudsAmbientProbe, sampler_VolumetricCloudsAmbientProbe, half3(0.0, 1.0, 0.0), 4.0).rgb;
                half3 ambientTermBottom = SAMPLE_TEXTURECUBE_LOD(_VolumetricCloudsAmbientProbe, sampler_VolumetricCloudsAmbientProbe, half3(0.0, -1.0, 0.0), 4.0).rgb;
            #else
                half3 ambientTermTop = EvaluateVolumetricCloudsAmbientProbe(half3(0.0, 1.0, 0.0));
                half3 ambientTermBottom = EvaluateVolumetricCloudsAmbientProbe(half3(0.0, -1.0, 0.0));
            #endif
                half3 ambient = max(0, lerp(ambientTermBottom, ambientTermTop, relativeHeight) * _AmbientProbeDimmer);

                volumetricRay.scattering = sunColor * volumetricRay.scattering;
                volumetricRay.scattering += ambient * volumetricRay.ambient;
            }
        }
    }
    return volumetricRay;
}

// ── TORUS DEBUG ─────────────────────────────────────────────────
// Call DebugTorusRay() from frag to overlay debug colours.
// mode 0 = off, 1 = green/red intersection, 2 = pure white (shader alive)
float _TorusDebugMode;

VolumetricRayResult DebugTorusRay(CloudRay cloudRay)
{
    VolumetricRayResult result;
    ZERO_INITIALIZE(VolumetricRayResult, result);
    result.transmittance = 1.0;
    result.invalidRay    = true;

    if (_TorusDebugMode < 0.5) return result;

    // Mode 2: pure white — confirms shader is running
    if (_TorusDebugMode > 1.5)
    {
        result.scattering   = half3(1.0, 1.0, 1.0);
        result.transmittance = 0.0;
        result.meanDistance  = 100.0;
        result.invalidRay    = false;
        return result;
    }

    // Mode 1: test if ray enters torus tube
    float3 relOrigin = cloudRay.originWS - _TorusCenter;
    float R = _TorusMajorRadius;
    float r = _TorusMinorRadius;

    // Step along ray and check torus implicit
    bool hit = false;
    for (int i = 0; i < 64; i++)
    {
        float t = float(i) * (R * 2.0 / 64.0);
        float3 p = relOrigin + cloudRay.direction * t;
        if (TorusImplicit(p, R, r) < 0.0) { hit = true; break; }
    }

    result.scattering    = hit ? half3(0.0, 1.0, 0.0) : half3(1.0, 0.0, 0.0);
    result.transmittance = 0.0;
    result.meanDistance  = 100.0;
    result.invalidRay    = false;
    return result;
}
// ────────────────────────────────────────────────────────────────

#endif