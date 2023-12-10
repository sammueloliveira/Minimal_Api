using Infra.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MiniValidation;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;
using Presentation.Models;

var builder = WebApplication.CreateBuilder(args);

#region Configure Services

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Minimal_Api",
        Version = "v1",
        Description = "Descri��o da API",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT desta maneira: Bearer {seu token}",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });

});

builder.Services.AddDbContext<Contexto>(options => 
options.UseSqlServer(builder.Configuration.GetConnectionString("Connection")));

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options => 
options.UseSqlServer(builder.Configuration.GetConnectionString("Connection"),
    b => b.MigrationsAssembly("Infra")));

builder.Services.AddIdentityConfiguration();

builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ExcluirFornecedor",
        policy => policy.RequireClaim("ExcluirFornecedor"));
});

#endregion 

#region Configure Pipeline
var app = builder.Build();

 if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

MapActions(app);

app.Run();

#endregion

#region Actions
void MapActions(WebApplication app)
{
    app.MapPost("/registro", [AllowAnonymous] async (SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings, RegisterUser registerUser) =>
    {
        if (registerUser == null) return Results.BadRequest("Usuario nao informado!");

        if (!MiniValidator.TryValidate(registerUser, out var errors))
            return Results.ValidationProblem(errors);

        var user = new IdentityUser
        {
            UserName = registerUser.Email,
            Email = registerUser.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, registerUser.Password);
        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var jwt = new JwtBuilder()
        .WithUserManager(userManager)
        .WithJwtSettings(appJwtSettings.Value)
        .WithEmail(user.Email)
        .WithJwtClaims()
        .WithUserClaims()
        .WithUserRoles()
        .BuildUserResponse();

        return Results.Ok(jwt);

    }).ProducesValidationProblem()
   .Produces(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status400BadRequest)
   .WithName("RegistroUsuario")
   .WithTags("Usuario");

    app.MapPost("/login", [AllowAnonymous] async (SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings, LoginUser loginUser) =>
    {
        if (loginUser == null) return Results.BadRequest("Usaurio nao informado!");

        if (!MiniValidator.TryValidate(loginUser, out var errors))
            return Results.ValidationProblem(errors);


        var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password,
            false, true);
        if (result.IsLockedOut)
            return Results.BadRequest("Usuario bloqueado");

        if (!result.Succeeded)
            return Results.BadRequest("Usuario e/ou senha invalido!");

        var jwt = new JwtBuilder()
        .WithUserManager(userManager)
        .WithJwtSettings(appJwtSettings.Value)
        .WithEmail(loginUser.Email)
        .WithJwtClaims()
        .WithUserClaims()
        .WithUserRoles()
        .BuildToken();

        return Results.Ok(jwt);

    }).ProducesValidationProblem()
       .Produces(StatusCodes.Status200OK)
       .Produces(StatusCodes.Status400BadRequest)
       .WithName("LoginUsuario")
       .WithTags("Usuario");



    app.MapGet("/fornecedor", [Authorize] async (Contexto contexto) =>
        await contexto.Fornecedores.ToListAsync())
        .WithName("GetFornecedor")
        .WithTags("Fornecedor");

    app.MapGet("/fornecedor/{id}", [Authorize] async (Guid id, Contexto contexto) =>
        await contexto.Fornecedores.FindAsync(id)
        is Fornecedor fornecedor ? Results.Ok(fornecedor)
        : Results.NotFound())
        .Produces<Fornecedor>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("GetFornecedorPorId")
        .WithTags("Fornecedor");

    app.MapPost("/fornecedor", [Authorize] async (Contexto contexto,
        Fornecedor fornecedor) =>
        {
        if (!MiniValidator.TryValidate(fornecedor, out var errors))
            return Results.ValidationProblem(errors);

        contexto.Fornecedores.Add(fornecedor);
        var result = await contexto.SaveChangesAsync();

        return result > 0
        ? Results.Created($"/fornecedor/ {fornecedor.Id}", fornecedor)
        : Results.BadRequest("Houve um problema ao salvar o registro");


        }).ProducesValidationProblem()
        .Produces<Fornecedor>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PostFornecedor")
        .WithTags("Fornecedor");

    app.MapPut("/fornecedor/{id}", [Authorize] async (Guid id,
        Contexto contexto, Fornecedor fornecedor) =>
        {
        var forncedorBanco = await contexto.Fornecedores.AsNoTracking<Fornecedor>()
        .FirstOrDefaultAsync(f => f.Id == id);
        if (forncedorBanco == null) return Results.NotFound();

        if (!MiniValidator.TryValidate(fornecedor, out var errors))
            return Results.ValidationProblem(errors);

        contexto.Fornecedores.Update(fornecedor);
        var result = await contexto.SaveChangesAsync();

        return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao salvar o registro");


         }).ProducesValidationProblem()
        .Produces<Fornecedor>(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PutFornecedor")
        .WithTags("Fornecedor");

    app.MapDelete("/fornecedor/{id}", [Authorize] async (Guid id,
        Contexto contexto) =>
        {
        var fornecedor = await contexto.Fornecedores.FindAsync(id);
        if (fornecedor == null) return Results.NotFound();

        if (!MiniValidator.TryValidate(fornecedor, out var errors))
            return Results.ValidationProblem(errors);

        contexto.Fornecedores.Remove(fornecedor);
        var result = await contexto.SaveChangesAsync();

        return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao salvar o registro");


        }).Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("DeleteFornecedor")
        .WithTags("Fornecedor");
}

#endregion
