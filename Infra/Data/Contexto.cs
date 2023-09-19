using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Presentation.Models;

namespace Infra.Data
{
    public  class Contexto : IdentityDbContext
    {
        public Contexto(DbContextOptions<Contexto> options) :base(options)
        {

        }
        public DbSet<Fornecedor> Fornecedores { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
                optionsBuilder.UseSqlServer(ObterConexao());
            base.OnConfiguring(optionsBuilder);
        }

        private string ObterConexao()
        {
            return "Data Source=samuel;Initial Catalog=Minima_Api;Integrated Security=True;Encrypt=false";
        }
    }
}
