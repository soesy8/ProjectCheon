if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Play 모드에서만 카메라 검증을 실행합니다.");
var camera = Camera.main;
camera.transform.position = new Vector3(-60f, 13f, 105f);
camera.transform.rotation = Quaternion.LookRotation(new Vector3(-10f, 22f, 20f) - camera.transform.position);
return new { frame = Time.frameCount, cameraHeight = camera.transform.position.y, fogTop = GameObject.Find("HeightFog_VFX").transform.position.y, shaderErrors = ShaderUtil.GetShaderMessages(Shader.Find("Cheon/VFX/Height Fog Volume")) };
