using UnityEngine;
namespace GamePlay.Player
{
    public static class PlayerAnimatorName
    {
        public static readonly int run_e=Animator.StringToHash("tp_run_e_knife");
        public static readonly int run_w=Animator.StringToHash("tp_run_w_knife");
        public static readonly int run_s=Animator.StringToHash("tp_run_s_knife");
        public static readonly int run_n=Animator.StringToHash("tp_run_n_knife");
        public static readonly int idle=Animator.StringToHash("tp_draw_butterfly");
        public static readonly int draw_ak=Animator.StringToHash("tp_draw_ak");
        public static readonly int draw_butterfly=Animator.StringToHash("tp_draw_butterfly");



        public static class FP
        {
            public static readonly int draw_ak=Animator.StringToHash("fp_draw_ak");
            public static readonly int draw_butterfly=Animator.StringToHash("fp_draw_butterfly");
        }
    }
}
