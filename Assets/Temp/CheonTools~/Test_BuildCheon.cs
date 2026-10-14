UnityEditor.EditorApplication.delayCall += () =>
{
    try
    {
        Test_CheonSceneBuilder.Build();
        System.IO.File.WriteAllText("Assets/Temp/Test_CheonBuildResult.txt", "PASS: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
    }
    catch (System.Exception exception)
    {
        System.IO.File.WriteAllText("Assets/Temp/Test_CheonBuildResult.txt", exception.ToString());
        UnityEngine.Debug.LogException(exception);
    }
};
return "Scene build scheduled";
