namespace DS4MTHACK
{
    partial class MainForm : Form
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();

                if (fakerInput != null)
                {
                    fakerInput.Dispose();
                    fakerInput = null;
                }

                InputMasker.ControllerConnected = false;
                InputMasker.StopHook();
                AntiDetection.RemoveApiHooks();
            }
            base.Dispose(disposing);
        }


        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.Text = "DS4 Virtual Controller";
            this.ResumeLayout(false);
        }

        #endregion
    }
}
