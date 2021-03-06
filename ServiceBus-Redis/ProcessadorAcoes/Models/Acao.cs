using System;

namespace ProcessadorAcoes.Models
{
    public class Acao
    {
        private string _codigo;
        public string Codigo
        {
            get => _codigo;
            set => _codigo = value?.Trim().ToUpper();
        }

        public double? Valor { get; set; }
 
        private string _codCorretora;
        public string CodCorretora
        {
            get => _codCorretora;
            set => _codCorretora = value?.Trim().ToUpper();
        }

        public string NomeCorretora { get; set; }
        public DateTime Data { get; set; } = DateTime.Now;
    }
}
