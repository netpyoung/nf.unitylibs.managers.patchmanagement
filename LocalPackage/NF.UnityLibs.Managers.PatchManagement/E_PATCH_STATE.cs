namespace NF.UnityLibs.Managers.PatchManagement
{ 
    public enum E_PATCH_STATE
    {
        NONE = 0,
        RECIEVE_PATCHBUILDVERSION = 1,
        PATCHFILELIST_CURR = 2,
        PATCHFILELIST_NEXT = 3,
        PATCHFILELIST_DIFF_COLLECT_START = 4,
        PATCHFILELIST_DIFF_COLLECT_END = 5,
        PATCHFILELIST_VALIDATE = 6,
        PATCHFILELIST_FINALIZE = 7,
    }
}