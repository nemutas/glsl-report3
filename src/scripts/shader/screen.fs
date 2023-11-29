precision mediump float;

#define repeat(p, span) mod(p, span) - (0.5 * span)

uniform vec2 uResolution;
uniform float uTime;
uniform sampler2D tMatcap;

const float MIN_DIST = 0.001;
const float MAX_DIST = 20.0;
const float PI = acos(-1.0);
const float TAU = PI * 2.0;

#include './modules/matcap.glsl'
#include './modules/color.glsl'
#include './modules/primitives.glsl'
#include './modules/combinations.glsl'

mat2 rotate(float a) {
  float s = sin(a), c = cos(a);
  return mat2(c, s, -s, c);
}

vec2 pmod(vec2 p, float r) {
  float a = atan(p.x, p.y) + PI / r;
  float n = TAU / r;
  a = floor(a / n) * n;
  return p * rotate(-a);
}

vec2 sdf(vec3 p) {
  p.x -= 0.5;
  p.z -= 1.0;
  p.xz = rotate(p.z * 0.1 - 0.4) * p.xz;
  p.z -= uTime * 0.5;
  p.xy = rotate(-uTime * 0.15 + p.z * 0.1) * p.xy;

  p.xy = pmod(p.xy, 5.0);
  p.y -= 0.8;
  p.z = repeat(p.z, 2.0);

  vec3 pp = p;
  pp.xy = rotate(-pp.z * PI * 1.5) * pp.xy;

  p.x = abs(p.x);

  float width = 0.581;
  float b1 = sdBox(p, vec3(width + 0.1, 0.1, 0.05)) - 0.015;
  float c1 = sdCapsule(p, vec3(width, 0.0, 1.0), vec3(width, 0.0, -1.0), 0.08);
  float b2 = sdBox(pp, vec3(0.02, 0.02, 1.0)) - 0.01;
  float final = opSmoothUnion(b1, c1, 0.03);
  final = opSmoothUnion(final, b2, 0.02);

  float id = 0.0;
  if (abs(final - c1) < 0.01) {
    id = 1.0;
  } else if (abs(final - b2) < 0.01) {
    id = 2.0;
  }

  return vec2(final, id);
}

#include './modules/normal.glsl'
#include './modules/shadow.glsl'

void main() {
  vec2 p = (gl_FragCoord.xy * 2.0 - uResolution) / min(uResolution.x, uResolution.y);
  vec3 rd = normalize(vec3(p, -2.0));
  vec3 ro = vec3(0.0, 0.0, 5.0);
  vec3 ray = ro;
  vec2 data = vec2(0);
  float dist = 0.0;

  for (int i = 0; i < 100; i++) {
    data = sdf(ray);
    ray += rd * data.x;
    dist += data.x;

    if (abs(data.x) < MIN_DIST || MAX_DIST < dist)
      break;
  }

  vec3 bg = vec3(0);
  vec3 color = bg;

  if (dist < MAX_DIST) {
    vec3 light = vec3(5.0, 3.0, 3.0);
    vec3 normal = calcNormal(ray);

    color = vec3(1.0);

    vec2 matcapUv = matcap(rd, normal);
    vec3 mc = texture2D(tMatcap, matcapUv).rgb;
    color *= mc;

    vec3 hsv = rgb2hsv(color);
    hsv.g = 0.0;
    color = hsv2rgb(hsv);

    if (data.y == 0.0) {
      color *= vec3(0.61, 0.53, 0.00);
    } else if (data.y == 1.0) {
      color *= vec3(0.1);
    } else {
      color *= vec3(1.0);
    }
    
    float diffuse = max(dot(normal, normalize(light)), 0.5);
    color *= pow(diffuse + 0.3, 3.0);

    float fresnel = 1.0 - pow(1.0 + dot(rd, normal), 5.0);
    color = mix(bg, color, fresnel);

    float fog = 1.0 - smoothstep(5.0, 12.0, dist);
    fog *= smoothstep(0.8, 5.0, dist);
    color = mix(bg, color, fog);

    float shadow = softShadow(ray + normal * 0.01, light, 8.0);
    color = mix(color * vec3(0.05), color, shadow);
  }

  gl_FragColor = vec4(color, 1.0);
}