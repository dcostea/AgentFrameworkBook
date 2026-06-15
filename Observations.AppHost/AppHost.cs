var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Observations>("observations")
  .WithExplicitStart();

builder.Build().Run();
