using Docker.DotNet;
using Docker.DotNet.Models;

var client = new DockerClientConfiguration().CreateClient();

var filters = new Dictionary<string, IDictionary<string, bool>>
{
    ["label"] = new Dictionary<string, bool>
    {
        { "classinsights.update", true }
    }
};

var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
{
    Filters = filters,
    All = true
});

foreach (var container in containers)
{
    try
    {
        Console.WriteLine($"Updating {container.ID}");

        // pull the latest image
        await client.Images.CreateImageAsync(new ImagesCreateParameters
        {
            FromImage = container.Image,
            Tag = "latest"
        }, new AuthConfig(), new Progress<JSONMessage>(msg => Console.WriteLine(msg.Status)));
        
        // prevent termination of own container
        if (container.ID.StartsWith(Environment.MachineName))
            continue;

        // inspect old container
        var inspect = await client.Containers.InspectContainerAsync(container.ID);

        // stop and remove the old container
        await client.Containers.StopContainerAsync(container.ID, new ContainerStopParameters
        {
            WaitBeforeKillSeconds = 10
        });
        await client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters
        {
            Force = true,
            RemoveVolumes = true
        });

        // recreate with the same settings
        var created = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = inspect.Name,
            Image = inspect.Config.Image,
            Env = inspect.Config.Env,
            ExposedPorts = inspect.Config.ExposedPorts,
            HostConfig = inspect.HostConfig,
            Cmd = inspect.Config.Cmd,
            Volumes = inspect.Config.Volumes,
            NetworkingConfig = new NetworkingConfig { EndpointsConfig = inspect.NetworkSettings.Networks },
            Labels = inspect.Config.Labels,
            WorkingDir = inspect.Config.WorkingDir,
            Hostname = inspect.Config.Hostname,
            Domainname = inspect.Config.Domainname,
            User = inspect.Config.User,
            AttachStdin = inspect.Config.AttachStdin,
            AttachStdout = inspect.Config.AttachStdout,
            AttachStderr = inspect.Config.AttachStderr,
            Tty = inspect.Config.Tty,
            OpenStdin = inspect.Config.OpenStdin,
            StdinOnce = inspect.Config.StdinOnce,
            Entrypoint = inspect.Config.Entrypoint,
            MacAddress = inspect.Config.MacAddress,
            ArgsEscaped = inspect.Config.ArgsEscaped,
            Healthcheck = inspect.Config.Healthcheck,
            NetworkDisabled = inspect.Config.NetworkDisabled,
            OnBuild = inspect.Config.OnBuild,
            StopSignal = inspect.Config.StopSignal,
            StopTimeout = inspect.Config.StopTimeout,
            Shell = inspect.Config.Shell,
            Platform = inspect.Platform
        });

        // start the new container
        await client.Containers.StartContainerAsync(created.ID, new ContainerStartParameters());
        Console.WriteLine($"Replaced {container.ID} → {created.ID}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error updating {container.ID}: {e.Message}");
    }
}