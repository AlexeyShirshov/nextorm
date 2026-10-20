## .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3 (Job: DefaultJob)

```assembly
; NextORM.Benchmark.SqliteBenchmarkStageA.Where_CachedHit_PlanOnly()
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,0B8
       vzeroupper
       lea       rbp,[rsp+0E0]
       mov       rbx,rdi
       xor       r15d,r15d
       jmp       near ptr M00_L28
M00_L00:
       mov       rdi,rax
       mov       rcx,[rax]
       mov       rcx,[rcx+70]
       call      qword ptr [rcx+18]
       test      al,20
       jne       near ptr M00_L05
M00_L01:
       inc       r12d
M00_L02:
       mov       edi,[r13+8]
       cmp       edi,r12d
       jle       near ptr M00_L116
       cmp       r12d,edi
       jae       near ptr M00_L125
       mov       edi,r12d
       mov       rax,[r13+rdi*8+10]
       mov       [rbp-90],rax
       mov       rdi,rax
       mov       rcx,[rax]
       mov       rcx,[rcx+50]
       call      qword ptr [rcx+28]
       test      eax,eax
       je        short M00_L03
       mov       rdi,[rbp-90]
       mov       esi,1
       mov       rax,[rdi]
       mov       rax,[rax+58]
       call      qword ptr [rax+8]
       mov       [rbp-98],rax
       mov       rdi,r14
       mov       rsi,rax
       mov       rcx,[r14]
       mov       rcx,[rcx+40]
       mov       [rbp-30],rcx
       call      qword ptr [rcx+10]
       test      eax,eax
       jne       near ptr M00_L14
       mov       rdi,r14
       mov       rax,[rbp-30]
       call      qword ptr [rax+38]
       mov       [rbp-0A0],rax
       mov       rdi,offset MT_System.RuntimeType
       mov       rcx,rdi
       cmp       [rax],rcx
       je        near ptr M00_L10
       mov       rdi,rax
       mov       rcx,[rax]
       mov       rcx,[rcx+70]
       call      qword ptr [rcx+18]
       test      al,20
       jne       near ptr M00_L12
M00_L03:
       mov       rdi,[rbp-90]
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+30]
       test      eax,eax
       je        near ptr M00_L01
       mov       rdi,[rbp-90]
       mov       esi,1
       mov       rax,[rdi]
       mov       rax,[rax+58]
       call      qword ptr [rax+18]
       mov       [rbp-0B0],rax
       mov       rdi,r14
       mov       rsi,rax
       mov       rcx,[r14]
       mov       rcx,[rcx+40]
       mov       [rbp-30],rcx
       call      qword ptr [rcx+10]
       test      eax,eax
       jne       near ptr M00_L14
       mov       rdi,r14
       mov       rax,[rbp-30]
       call      qword ptr [rax+38]
       mov       [rbp-0B8],rax
       mov       rcx,offset MT_System.RuntimeType
       cmp       [rax],rcx
       jne       near ptr M00_L00
       test      rax,rax
       je        near ptr M00_L00
       mov       rdi,[rax+18]
       test      dil,2
       jne       near ptr M00_L88
       mov       edi,[rdi]
       and       edi,0F0000
       cmp       edi,0C0000
       sete      dil
       movzx     edi,dil
M00_L04:
       test      edi,edi
       je        near ptr M00_L01
M00_L05:
       mov       rdi,r14
       mov       rax,[rbp-30]
       call      qword ptr [rax+30]
       mov       [rbp-0C0],rax
       mov       rdi,[rbp-0B0]
       mov       rcx,[rdi]
       mov       rcx,[rcx+40]
       call      qword ptr [rcx+30]
       mov       rsi,rax
       mov       rdx,[rbp-0C0]
       cmp       rdx,rsi
       jne       short M00_L07
M00_L06:
       mov       rdi,r14
       mov       rax,[rbp-30]
       call      qword ptr [rax+30]
       test      rax,rax
       je        near ptr M00_L115
       xor       edi,edi
       mov       [rsp],rdi
       mov       rdi,[rbp-0B8]
       mov       rsi,rax
       mov       edx,1C
       xor       ecx,ecx
       mov       r8d,3
       xor       r9d,r9d
       mov       rax,[rdi]
       mov       rax,[rax+88]
       call      qword ptr [rax+38]
       mov       rsi,[rbp-0B0]
       cmp       rax,rsi
       je        near ptr M00_L14
       test      rax,rax
       je        near ptr M00_L01
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       test      eax,eax
       jne       near ptr M00_L14
       jmp       near ptr M00_L01
M00_L07:
       test      rdx,rdx
       je        near ptr M00_L01
       test      rsi,rsi
       je        near ptr M00_L01
       mov       edi,[rdx+8]
       cmp       edi,[rsi+8]
       jne       near ptr M00_L01
       lea       rdi,[rdx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       add       rsi,0C
       call      qword ptr [75C475047A38]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       test      eax,eax
       je        near ptr M00_L01
       jmp       near ptr M00_L06
M00_L08:
       mov       r14d,1
       jmp       near ptr M00_L18
M00_L09:
       mov       edi,3067
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476F25200]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M00_L10:
       mov       rdi,[rax+18]
       test      dil,2
       jne       near ptr M00_L87
       mov       edi,[rdi]
       and       edi,0F0000
       cmp       edi,0C0000
       sete      dil
       movzx     edi,dil
M00_L11:
       test      edi,edi
       je        near ptr M00_L03
M00_L12:
       mov       rdi,r14
       mov       rcx,[rbp-30]
       call      qword ptr [rcx+30]
       mov       [rbp-0A8],rax
       mov       rdi,[rbp-98]
       mov       rcx,[rdi]
       mov       rcx,[rcx+40]
       call      qword ptr [rcx+30]
       mov       rsi,rax
       mov       rdx,[rbp-0A8]
       cmp       rdx,rsi
       jne       near ptr M00_L61
M00_L13:
       mov       rdi,r14
       mov       rax,[rbp-30]
       call      qword ptr [rax+30]
       test      rax,rax
       je        near ptr M00_L115
       xor       edi,edi
       mov       [rsp],rdi
       mov       rdi,[rbp-0A0]
       mov       rsi,rax
       mov       edx,1C
       xor       ecx,ecx
       mov       r8d,3
       xor       r9d,r9d
       mov       rax,[rdi]
       mov       rax,[rax+88]
       call      qword ptr [rax+38]
       mov       rsi,[rbp-98]
       cmp       rax,rsi
       je        short M00_L14
       test      rax,rax
       je        near ptr M00_L03
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       test      eax,eax
       je        near ptr M00_L03
M00_L14:
       mov       [rbp-60],rbx
       mov       rdi,rbx
       mov       rsi,[rbp-90]
       call      qword ptr [75C47607E298]; System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.PropertyInfo)
       mov       [rbp-68],rax
       mov       r13,75C47040A290
       mov       [rbp-0D0],r13
       mov       r12,r13
M00_L15:
       mov       rcx,offset MT_System.RuntimeType
       mov       rax,rcx
       mov       [rbp-38],rax
       cmp       [r12],rax
       jne       near ptr M00_L91
       mov       [rbp-0C8],r12
       mov       rdi,[r12+18]
       mov       rcx,75C4F3474CD0
       call      rcx
       movzx     ebx,al
       mov       r12,[rbp-0C8]
       cmp       dword ptr [75C4F36CEC80],0
       jne       near ptr M00_L90
M00_L16:
       cmp       ebx,1D
       ja        short M00_L17
       mov       edi,1FEF7FFF
       bt        edi,ebx
       jae       near ptr M00_L08
M00_L17:
       cmp       ebx,10
       sete      r14b
       movzx     r14d,r14b
M00_L18:
       test      r14d,r14d
       jne       near ptr M00_L89
       mov       r14,[rbp-38]
       cmp       [r12],r14
       jne       near ptr M00_L92
M00_L19:
       test      r12,r12
       je        near ptr M00_L117
       mov       rdi,r12
       call      000075C4F34FE6D0
       test      eax,eax
       jne       near ptr M00_L118
       mov       rdi,offset MT_NextORM.Benchmark.SqliteBenchmarkStageA+<>c__DisplayClass8_0
       mov       rax,75C4F3474CD0
       call      rax
       movzx     ebx,al
       cmp       dword ptr [75C4F36CEC80],0
       jne       near ptr M00_L93
M00_L20:
       cmp       ebx,10
       je        near ptr M00_L121
       mov       rdi,offset MT_NextORM.Benchmark.SqliteBenchmarkStageA+<>c__DisplayClass8_0
       mov       rax,75C4F3474CD0
       call      rax
       movzx     ebx,al
       mov       r13,[rbp-0D0]
       cmp       dword ptr [75C4F36CEC80],0
       jne       near ptr M00_L94
M00_L21:
       cmp       ebx,0F
       je        near ptr M00_L122
       mov       rdi,[rbp-50]
       call      qword ptr [75C47543D758]; System.Object.GetType()
       cmp       rax,r13
       jne       near ptr M00_L95
       mov       rdi,offset MT_System.Linq.Expressions.ConstantExpression
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rdi,[rbx+8]
       mov       rsi,[rbp-50]
       call      CORINFO_HELP_ASSIGN_REF
M00_L22:
       mov       rdi,75C47679E928
       call      System.RuntimeFieldInfoStub.FromPtr(IntPtr)
       mov       rdi,rax
       call      qword ptr [75C476786328]; System.Reflection.FieldInfo.GetFieldFromHandle(System.RuntimeFieldHandle)
       mov       r13,rax
       test      r13,r13
       je        near ptr M00_L124
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+50]
       call      qword ptr [rax+10]
       test      al,10
       jne       near ptr M00_L09
       mov       rdi,rbx
       mov       rsi,75C470406580
       mov       edx,0FFFFFFFF
       call      qword ptr [75C47607E3A0]; System.Dynamic.Utils.ExpressionUtils.RequiresCanRead(System.Linq.Expressions.Expression, System.String, Int32)
       mov       rdi,offset MT_System.Reflection.RtFieldInfo
       cmp       [r13],rdi
       jne       near ptr M00_L97
       mov       rdi,[r13+8]
       cmp       byte ptr [rdi+9C],0
       jne       near ptr M00_L96
       mov       r14,[r13+10]
M00_L23:
       mov       rdi,offset MT_System.Linq.Expressions.ConstantExpression
       cmp       [rbx],rdi
       jne       near ptr M00_L99
       mov       rdi,[rbx+8]
       test      rdi,rdi
       je        near ptr M00_L98
       call      qword ptr [75C47543D758]; System.Object.GetType()
       mov       r12,rax
M00_L24:
       mov       rdi,r14
       mov       rsi,r12
       call      qword ptr [75C476787F90]; System.Dynamic.Utils.TypeUtils.AreEquivalent(System.Type, System.Type)
       test      eax,eax
       je        near ptr M00_L100
M00_L25:
       mov       rdi,offset MT_System.Linq.Expressions.FieldExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       lea       rdi,[r14+10]
       mov       rsi,r13
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdi,[rbp-68]
       mov       rsi,r14
       xor       edx,edx
       xor       ecx,ecx
       call      qword ptr [75C47607E838]; System.Linq.Expressions.Expression.Equal(System.Linq.Expressions.Expression, System.Linq.Expressions.Expression, Boolean, System.Reflection.MethodInfo)
       mov       r13,rax
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       mov       esi,1
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       rbx,rax
       lea       rdi,[rbx+10]
       mov       rsi,[rbp-60]
       call      CORINFO_HELP_ASSIGN_REF
       mov       r8,rbx
       mov       rsi,r13
       mov       rdi,75C4762A21D8
       xor       edx,edx
       xor       ecx,ecx
       call      qword ptr [75C47607E9A0]; System.Linq.Expressions.Expression.Lambda[[System.__Canon, System.Private.CoreLib]](System.Linq.Expressions.Expression, System.String, Boolean, System.Collections.Generic.IEnumerable`1<System.Linq.Expressions.ParameterExpression>)
       mov       rsi,rax
       mov       rdi,[rbp-58]
       cmp       [rdi],edi
       call      qword ptr [75C47543EC28]; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].Where(System.Linq.Expressions.Expression`1<System.Func`2<System.__Canon,Boolean>>)
       mov       rbx,rax
       mov       r14,75C470402168
       mov       rdi,75C470402168
       mov       esi,1
       call      qword ptr [75C47607DFB0]; System.Linq.Expressions.Expression.Validate(System.Type, Boolean)
       mov       rdi,75C470402168
       call      qword ptr [75C475434948]; System.RuntimeType.GetCorElementType()
       cmp       eax,10
       sete      r13b
       movzx     r13d,r13b
       test      r13d,r13d
       jne       near ptr M00_L102
M00_L26:
       mov       rdi,r14
       mov       edx,r13d
       mov       rsi,75C470402190
       call      qword ptr [75C47607E028]; System.Linq.Expressions.ParameterExpression.Make(System.Type, System.String, Boolean)
       mov       r14,rax
       mov       rdi,75C4754DDC30
       call      System.RuntimeMethodInfoStub.FromPtr(IntPtr)
       mov       rdi,rax
       call      qword ptr [75C47543EBB0]; System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)
       mov       rsi,rax
       test      rsi,rsi
       je        short M00_L27
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [rsi],rdi
       jne       near ptr M00_L103
M00_L27:
       mov       rdi,r14
       call      qword ptr [75C47543EBC8]; System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.MethodInfo)
       mov       r13,rax
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       mov       esi,1
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       r12,rax
       lea       rdi,[r12+10]
       mov       rsi,r14
       call      CORINFO_HELP_ASSIGN_REF
       mov       r8,r12
       mov       rsi,r13
       mov       rdi,75C4762C4230
       xor       edx,edx
       xor       ecx,ecx
       call      qword ptr [75C47607E9A0]; System.Linq.Expressions.Expression.Lambda[[System.__Canon, System.Private.CoreLib]](System.Linq.Expressions.Expression, System.String, Boolean, System.Collections.Generic.IEnumerable`1<System.Linq.Expressions.ParameterExpression>)
       mov       rdx,rax
       mov       rdi,rbx
       mov       rsi,75C475945E48
       cmp       [rdi],edi
       call      qword ptr [75C47543E298]; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].Select[[System.Int32, System.Private.CoreLib]](System.Linq.Expressions.Expression`1<System.Func`2<System.__Canon,Int32>>)
       mov       rbx,rax
       mov       rdi,[rbp-48]
       mov       rsi,offset MT_NextORM.Core.IQueryPlanner
       mov       rdx,75C4759527B8
       call      System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointer(System.Object, IntPtr, IntPtr)
       mov       rdi,[rbp-48]
       mov       rsi,rbx
       xor       edx,edx
       mov       ecx,1
       xor       r8d,r8d
       call      rax
       inc       r15d
       cmp       r15d,64
       mov       rbx,[rbp-40]
       jge       near ptr M00_L60
M00_L28:
       mov       r14,[rbx+8]
       mov       [rbp-48],r14
       mov       rdi,offset MT_NextORM.Benchmark.SqliteBenchmarkStageA+<>c__DisplayClass8_0
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       [rbp-50],r13
       mov       [r13+8],r15d
       mov       [rbp-40],rbx
       mov       rdi,[rbx+10]
       mov       r12,[rdi+10]
       mov       [rbp-58],r12
       mov       rax,75C470402168
       mov       [rbp-70],rax
       mov       rcx,75C470402168
       mov       rdi,rcx
M00_L29:
       mov       rcx,offset MT_System.RuntimeType
       mov       [rbp-38],rcx
       cmp       [rdi],rcx
       jne       near ptr M00_L64
       mov       [rbp-78],rdi
       mov       rdx,[rdi+18]
       mov       rdi,rdx
       mov       rdx,75C4F3474CD0
       call      rdx
       movzx     ebx,al
       mov       r14,[rbp-78]
       cmp       dword ptr [75C4F36CEC80],0
       jne       near ptr M00_L63
M00_L30:
       cmp       ebx,1D
       ja        short M00_L31
       mov       edi,1FEF7FFF
       bt        edi,ebx
       jae       near ptr M00_L38
M00_L31:
       cmp       ebx,10
       sete      r13b
       movzx     r13d,r13b
M00_L32:
       test      r13d,r13d
       jne       near ptr M00_L62
       mov       r13,[rbp-38]
       cmp       [r14],r13
       jne       near ptr M00_L65
M00_L33:
       test      r14,r14
       je        near ptr M00_L104
       mov       rdi,r14
       call      000075C4F34FE6D0
       test      eax,eax
       jne       near ptr M00_L105
       mov       rdi,75C470402168
       mov       rax,[75C47405A0E0]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M00_L122
       mov       rdi,offset MT_NextORM.Benchmark.SimpleEntity
       mov       rax,75C4F3474CD0
       call      rax
       movzx     eax,al
       mov       rcx,75C470402168
       cmp       eax,10
       sete      bl
       movzx     ebx,bl
       cmp       dword ptr [75C4F36CEC80],0
       jne       near ptr M00_L66
M00_L34:
       test      ebx,ebx
       mov       r14,[rbp-70]
       jne       near ptr M00_L67
M00_L35:
       mov       rdi,r14
       mov       edx,ebx
       mov       rsi,75C470402190
       call      qword ptr [75C47607E028]; System.Linq.Expressions.ParameterExpression.Make(System.Type, System.String, Boolean)
       mov       [rbp-60],rax
       mov       rdi,75C4754DDC30
       call      System.RuntimeMethodInfoStub.FromPtr(IntPtr)
       mov       r14,rax
       test      r14,r14
       je        near ptr M00_L108
       mov       rbx,[rbp-60]
       mov       rdi,offset MT_System.RuntimeMethodInfoStub
       cmp       [r14],rdi
       jne       near ptr M00_L39
       mov       rsi,[r14+50]
M00_L36:
       xor       edi,edi
       call      qword ptr [75C475D05E18]; System.RuntimeType.GetMethodBase(System.RuntimeType, System.RuntimeMethodHandleInternal)
       mov       r12,rax
       test      r12,r12
       je        near ptr M00_L40
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r12],rdi
       jne       near ptr M00_L69
       mov       rdi,[r12+8]
       cmp       byte ptr [rdi+9C],0
       jne       near ptr M00_L68
       mov       r14,[r12+38]
M00_L37:
       test      r14,r14
       je        short M00_L44
       cmp       [r14],r13
       jne       near ptr M00_L71
       mov       rdi,[r14+18]
       test      dil,2
       jne       near ptr M00_L70
       test      dword ptr [rdi],80000000
       jne       short M00_L41
       test      byte ptr [rdi],30
       setne     al
       movzx     eax,al
       jmp       short M00_L42
M00_L38:
       mov       r13d,1
       jmp       near ptr M00_L32
M00_L39:
       mov       rdi,r14
       mov       r11,75C474062DF8
       call      qword ptr [r11]
       mov       rsi,rax
       jmp       near ptr M00_L36
M00_L40:
       xor       r14d,r14d
       jmp       short M00_L37
M00_L41:
       xor       eax,eax
M00_L42:
       movzx     eax,al
M00_L43:
       test      eax,eax
       jne       near ptr M00_L109
M00_L44:
       mov       r14,r12
       test      r14,r14
       je        short M00_L45
       mov       rsi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r14],rsi
       jne       near ptr M00_L72
M00_L45:
       mov       [rbp-80],r14
       test      r14,r14
       je        near ptr M00_L110
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r14],rdi
       jne       near ptr M00_L85
       mov       rdi,r14
       mov       rax,[rdi+8]
       movzx     eax,byte ptr [rax+9C]
       test      eax,eax
       jne       near ptr M00_L54
       mov       rcx,[rdi+38]
       test      rcx,rcx
       je        near ptr M00_L54
       test      eax,eax
       jne       near ptr M00_L73
       mov       rdi,[rdi+38]
       mov       r12,rdi
M00_L46:
       cmp       [r12],r13
       jne       near ptr M00_L78
M00_L47:
       cmp       [r12],r13
       jne       near ptr M00_L76
       mov       [rbp-60],rbx
       mov       [rbp-88],r12
       mov       rdi,[r12+18]
       mov       rax,75C4F3474CD0
       call      rax
       movzx     ebx,al
       mov       r12,[rbp-88]
       cmp       dword ptr [75C4F36CEC80],0
       jne       near ptr M00_L75
M00_L48:
       cmp       ebx,1D
       ja        short M00_L49
       mov       edi,1FEF7FFF
       bt        edi,ebx
       jae       near ptr M00_L57
M00_L49:
       cmp       ebx,10
       sete      r14b
       movzx     r14d,r14b
M00_L50:
       mov       rbx,[rbp-60]
M00_L51:
       test      r14d,r14d
       jne       near ptr M00_L74
       cmp       [r12],r13
       jne       near ptr M00_L77
M00_L52:
       test      r12,r12
       je        near ptr M00_L111
       mov       rdi,r12
       call      000075C4F34FE6D0
M00_L53:
       test      eax,eax
       jne       near ptr M00_L79
M00_L54:
       mov       r14,[rbp-80]
       mov       rdi,[r14+50]
       call      000075C4F35006C0
       test      eax,eax
       jne       near ptr M00_L81
M00_L55:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+40]
       mov       r13,rax
       call      qword ptr [r13+38]
       mov       r13,rax
       test      r13,r13
       je        near ptr M00_L116
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r14],rdi
       jne       near ptr M00_L86
       mov       r12d,[r14+5C]
M00_L56:
       test      r12b,10
       jne       short M00_L58
       mov       esi,4
       jmp       short M00_L59
M00_L57:
       mov       r14d,1
       jmp       near ptr M00_L50
M00_L58:
       mov       esi,8
M00_L59:
       or        esi,30
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+90]
       call      qword ptr [rax+38]
       mov       r13,rax
       xor       r12d,r12d
       jmp       near ptr M00_L02
M00_L60:
       add       rsp,0B8
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M00_L61:
       test      rdx,rdx
       je        near ptr M00_L03
       test      rsi,rsi
       je        near ptr M00_L03
       mov       edi,[rdx+8]
       cmp       edi,[rsi+8]
       jne       near ptr M00_L03
       lea       rdi,[rdx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       add       rsi,0C
       call      qword ptr [75C475047A38]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       test      eax,eax
       je        near ptr M00_L03
       jmp       near ptr M00_L13
M00_L62:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+68]
       call      qword ptr [rax+8]
       mov       r14,rax
       mov       rdi,r14
       jmp       near ptr M00_L29
M00_L63:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M00_L30
M00_L64:
       mov       [rbp-78],rdi
       mov       rdx,[rdi]
       mov       rdx,[rdx+68]
       call      qword ptr [rdx]
       mov       r13d,eax
       mov       r14,[rbp-78]
       jmp       near ptr M00_L32
M00_L65:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+98]
       call      qword ptr [rax+8]
       mov       r14,rax
       jmp       near ptr M00_L33
M00_L66:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M00_L34
M00_L67:
       mov       rdi,75C470402168
       mov       rax,[75C47405A0F0]
       call      qword ptr [rax+8]
       mov       r14,rax
       jmp       near ptr M00_L35
M00_L68:
       xor       r14d,r14d
       jmp       near ptr M00_L37
M00_L69:
       mov       rdi,r12
       mov       rax,[r12]
       mov       r14,[rax+40]
       call      qword ptr [r14+38]
       mov       r14,rax
       jmp       near ptr M00_L37
M00_L70:
       xor       eax,eax
       jmp       near ptr M00_L42
M00_L71:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+60]
       call      qword ptr [rax+8]
       jmp       near ptr M00_L43
M00_L72:
       mov       rsi,r12
       mov       rdi,offset MT_System.Reflection.MethodInfo
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       r14,rax
       jmp       near ptr M00_L45
M00_L73:
       xor       r12d,r12d
       jmp       near ptr M00_L46
M00_L74:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+68]
       call      qword ptr [rax+8]
       mov       r12,rax
       jmp       near ptr M00_L47
M00_L75:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M00_L48
M00_L76:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+68]
       call      qword ptr [rax]
       mov       r14d,eax
       jmp       near ptr M00_L51
M00_L77:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+98]
       call      qword ptr [rax+8]
       mov       r12,rax
       jmp       near ptr M00_L52
M00_L78:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+0B0]
       call      qword ptr [rax]
       jmp       near ptr M00_L53
M00_L79:
       mov       r14,[rbp-80]
M00_L80:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+58]
       call      qword ptr [rax+18]
       test      eax,eax
       jne       near ptr M00_L113
       jmp       near ptr M00_L112
M00_L81:
       mov       rdi,r14
       call      qword ptr [75C47607E790]; System.RuntimeMethodHandle.GetMethodInstantiationPublic(System.IRuntimeMethodInfo)
       mov       r13,rax
       test      r13,r13
       jne       short M00_L82
       mov       rdi,75BE85000310
       mov       r13,[rdi]
M00_L82:
       xor       r12d,r12d
       jmp       short M00_L84
M00_L83:
       mov       rdi,[r13+r12*8+10]
       mov       rax,[rdi]
       mov       rax,[rax+0B0]
       call      qword ptr [rax]
       test      eax,eax
       jne       short M00_L80
       inc       r12d
M00_L84:
       cmp       [r13+8],r12d
       jg        short M00_L83
       jmp       near ptr M00_L55
M00_L85:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+58]
       call      qword ptr [rax+28]
       test      eax,eax
       je        near ptr M00_L55
       jmp       short M00_L80
M00_L86:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+50]
       call      qword ptr [rax+20]
       mov       r12d,eax
       jmp       near ptr M00_L56
M00_L87:
       xor       edi,edi
       jmp       near ptr M00_L11
M00_L88:
       xor       edi,edi
       jmp       near ptr M00_L04
M00_L89:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+68]
       call      qword ptr [rax+8]
       mov       r12,rax
       jmp       near ptr M00_L15
M00_L90:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M00_L16
M00_L91:
       mov       rdi,r12
       mov       rcx,[r12]
       mov       rcx,[rcx+68]
       call      qword ptr [rcx]
       mov       r14d,eax
       jmp       near ptr M00_L18
M00_L92:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+98]
       call      qword ptr [rax+8]
       mov       r12,rax
       jmp       near ptr M00_L19
M00_L93:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M00_L20
M00_L94:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M00_L21
M00_L95:
       mov       rsi,rax
       mov       rdi,r13
       call      qword ptr [75C47405A518]; System.RuntimeType.IsAssignableFrom(System.Type)
       test      eax,eax
       je        near ptr M00_L123
       mov       rdi,offset MT_System.Linq.Expressions.TypedConstantExpression
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rdi,[rbx+8]
       mov       rsi,[rbp-50]
       call      CORINFO_HELP_ASSIGN_REF
       mov       [rbx+10],r13
       jmp       near ptr M00_L22
M00_L96:
       xor       r14d,r14d
       jmp       near ptr M00_L23
M00_L97:
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+40]
       call      qword ptr [rax+38]
       mov       r14,rax
       jmp       short M00_L99
M00_L98:
       mov       r12,75C470401D88
       jmp       near ptr M00_L24
M00_L99:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r12,rax
       jmp       near ptr M00_L24
M00_L100:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       jne       short M00_L101
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       jne       short M00_L101
       mov       rdi,r14
       mov       rsi,r12
       mov       rax,[r14]
       mov       rax,[rax+0B0]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M00_L25
M00_L101:
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+40]
       call      qword ptr [rax+38]
       mov       r15,rax
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       r14,rax
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rdx,rax
       mov       rdi,r15
       mov       rsi,r14
       call      qword ptr [75C476F25218]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M00_L102:
       mov       rdi,75C470402168
       mov       rax,[75C47405A0F0]
       call      qword ptr [rax+8]
       mov       r14,rax
       jmp       near ptr M00_L26
M00_L103:
       mov       rsi,rax
       mov       rdi,offset MT_System.Reflection.MethodInfo
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       rsi,rax
       jmp       near ptr M00_L27
M00_L104:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476E8F360]
       mov       rdx,rax
       mov       rdi,rbx
       xor       esi,esi
       call      qword ptr [75C476E8F378]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M00_L105:
       mov       rdi,75C470402168
       mov       rax,[75C47405A0E8]
       call      qword ptr [rax+10]
       test      eax,eax
       jne       near ptr M00_L106
       mov       rdi,75C4704027C0
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F738]
       mov       rbx,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,75C470406208
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,75C470402168
       call      qword ptr [75C476E8F768]
       mov       r14,rax
       mov       rdi,r15
       call      qword ptr [75C476F25AE8]
       lea       rdi,[r15+10]
       mov       rsi,r14
       call      CORINFO_HELP_ASSIGN_REF
       mov       dword ptr [r15+6C],80131501
       lea       rdi,[r15+70]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       mov       dword ptr [r15+6C],80070057
       jmp       near ptr M00_L107
M00_L106:
       mov       rdi,75C4704027C0
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F738]
       mov       r15,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       rdi,75C470406258
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,75C470402168
       call      qword ptr [75C476E8F768]
       mov       r14,rax
       mov       rdi,rbx
       call      qword ptr [75C476F25AE8]
       lea       rdi,[rbx+10]
       mov       rsi,r14
       call      CORINFO_HELP_ASSIGN_REF
       mov       dword ptr [rbx+6C],80131501
       lea       rdi,[rbx+70]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       mov       dword ptr [rbx+6C],80070057
       mov       r15,rbx
M00_L107:
       mov       rdi,r15
       call      CORINFO_HELP_THROW
       int       3
M00_L108:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476F24768]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C4760769D0]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M00_L109:
       call      qword ptr [75C476F24780]
       mov       rbx,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+68]
       call      qword ptr [rax+18]
       mov       rdx,rax
       mov       rsi,r12
       mov       rdi,rbx
       call      qword ptr [75C476F24798]
       mov       rsi,rax
       mov       rdi,r15
       call      qword ptr [75C4760769D0]
       mov       rdi,r15
       call      CORINFO_HELP_THROW
       int       3
M00_L110:
       mov       edi,3167
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F258]
       int       3
M00_L111:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476E8F360]
       mov       rdx,rax
       mov       rdi,rbx
       xor       esi,esi
       call      qword ptr [75C476E8F378]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M00_L112:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       rdi,75C4704064F0
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,r14
       call      qword ptr [75C476E8F768]
       mov       r14,rax
       mov       rdi,rbx
       call      qword ptr [75C476F25AE8]
       lea       rdi,[rbx+10]
       mov       rsi,r14
       call      CORINFO_HELP_ASSIGN_REF
       mov       dword ptr [rbx+6C],80131501
       mov       rdi,75C4704064B8
       mov       [rbx+70],rdi
       mov       dword ptr [rbx+6C],80070057
       jmp       short M00_L114
M00_L113:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       rdi,75C470406548
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,r14
       call      qword ptr [75C476E8F768]
       mov       r15,rax
       mov       rdi,rbx
       call      qword ptr [75C476F25AE8]
       lea       rdi,[rbx+10]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       mov       dword ptr [rbx+6C],80131501
       mov       rdi,75C4704064B8
       mov       [rbx+70],rdi
       mov       dword ptr [rbx+6C],80070057
M00_L114:
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M00_L115:
       mov       edi,5AD
       mov       rsi,75C474054000
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F258]
       int       3
M00_L116:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+40]
       mov       r13,rax
       call      qword ptr [r13+38]
       mov       rbx,rax
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       r14,rax
       mov       edi,3167
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdx,rax
       mov       rdi,rbx
       mov       rsi,r14
       mov       ecx,0FFFFFFFF
       call      qword ptr [75C476F248B8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M00_L117:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476E8F360]
       mov       rdx,rax
       mov       rdi,rbx
       xor       esi,esi
       call      qword ptr [75C476E8F378]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M00_L118:
       mov       r13,[rbp-0D0]
       mov       rdi,r13
       call      qword ptr [75C47405A288]; Precode of System.RuntimeType.get_IsGenericTypeDefinition()
       test      eax,eax
       jne       short M00_L119
       mov       rdi,75C4704027C0
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F738]
       mov       rbx,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,75C470406208
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,r13
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,r15
       mov       rdx,rbx
       call      qword ptr [75C4764A5CB0]
       jmp       short M00_L120
M00_L119:
       mov       rdi,75C4704027C0
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F738]
       mov       r15,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       rdi,75C470406258
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,r13
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,rbx
       mov       rdx,r15
       call      qword ptr [75C4764A5CB0]
       mov       r15,rbx
M00_L120:
       mov       rdi,r15
       call      CORINFO_HELP_THROW
       int       3
M00_L121:
       mov       edi,2ECB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F780]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M00_L122:
       mov       edi,2ECB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F798]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M00_L123:
       call      qword ptr [75C476F251A0]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M00_L124:
       mov       edi,3189
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F258]
       int       3
M00_L125:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 5024
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       push      rbp
       mov       rbp,rsp
       cmp       rdx,8
       jb        short M01_L01
       cmp       rdi,rsi
       je        near ptr M01_L11
       cmp       rdx,20
       jae       short M01_L05
       cmp       rdx,10
       jae       near ptr M01_L08
       add       rdx,0FFFFFFFFFFFFFFF8
       mov       rax,[rdi]
       sub       rax,[rsi]
       mov       rdi,[rdi+rdx]
       sub       rdi,[rsi+rdx]
       or        rax,rdi
       sete      al
       movzx     eax,al
M01_L00:
       vzeroupper
       pop       rbp
       ret
M01_L01:
       cmp       rdx,4
       jae       short M01_L04
       xor       eax,eax
       mov       rcx,rdx
       and       rcx,2
       je        short M01_L02
       movzx     eax,word ptr [rdi]
       movzx     r8d,word ptr [rsi]
       sub       eax,r8d
M01_L02:
       test      dl,1
       je        short M01_L03
       movzx     edx,byte ptr [rdi+rcx]
       movzx     edi,byte ptr [rsi+rcx]
       sub       edx,edi
       or        eax,edx
M01_L03:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       short M01_L00
M01_L04:
       lea       rax,[rdx-4]
       mov       ecx,[rdi]
       sub       ecx,[rsi]
       mov       edx,[rdi+rax]
       sub       edx,[rsi+rax]
       or        ecx,edx
       sete      al
       movzx     eax,al
       jmp       short M01_L00
M01_L05:
       xor       eax,eax
       add       rdx,0FFFFFFFFFFFFFFE0
       je        short M01_L07
M01_L06:
       vmovups   ymm0,[rdi+rax]
       vpcmpeqb  ymm0,ymm0,[rsi+rax]
       vpmovmskb ecx,ymm0
       cmp       ecx,0FFFFFFFF
       jne       short M01_L12
       add       rax,20
       cmp       rdx,rax
       ja        short M01_L06
M01_L07:
       vmovups   ymm0,[rdi+rdx]
       vpcmpeqb  ymm0,ymm0,[rsi+rdx]
       vpmovmskb eax,ymm0
       cmp       eax,0FFFFFFFF
       jne       short M01_L12
       jmp       short M01_L11
M01_L08:
       xor       eax,eax
       add       rdx,0FFFFFFFFFFFFFFF0
       je        short M01_L10
M01_L09:
       vmovups   xmm0,[rdi+rax]
       vpcmpeqb  xmm0,xmm0,[rsi+rax]
       vpmovmskb ecx,xmm0
       cmp       ecx,0FFFF
       jne       short M01_L12
       add       rax,10
       cmp       rdx,rax
       ja        short M01_L09
M01_L10:
       vmovups   xmm0,[rdi+rdx]
       vpcmpeqb  xmm0,xmm0,[rsi+rdx]
       vpmovmskb edi,xmm0
       cmp       edi,0FFFF
       jne       short M01_L12
M01_L11:
       mov       eax,1
       vzeroupper
       pop       rbp
       ret
M01_L12:
       xor       eax,eax
       vzeroupper
       pop       rbp
       ret
; Total bytes of code 281
```
```assembly
; System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.PropertyInfo)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       push      rax
       lea       rbp,[rsp+30]
       mov       rbx,rdi
       mov       r15,rsi
       test      r15,r15
       je        near ptr M02_L18
       mov       rdi,r15
       mov       esi,1
       mov       rax,[r15]
       mov       rax,[rax+58]
       call      qword ptr [rax+8]
       mov       r14,rax
       test      r14,r14
       je        near ptr M02_L19
       mov       rdi,75BE85000EC8
       mov       r13,[rdi]
       mov       rdi,offset MT_System.RuntimeType
       cmp       [r14],rdi
       jne       near ptr M02_L21
       mov       rdi,r14
       call      000075C4F32E52F0
       test      eax,eax
       je        near ptr M02_L11
M02_L00:
       mov       rdi,[r13+8]
       mov       esi,eax
       and       esi,[r13+10]
       cmp       esi,[rdi+8]
       jae       near ptr M02_L45
       mov       r12,[rdi+rsi*8+10]
       test      r12,r12
       je        near ptr M02_L23
       cmp       [r12+18],eax
       jne       near ptr M02_L23
       mov       rdi,[r12+8]
       mov       rsi,offset MT_System.RuntimeType
       cmp       [rdi],rsi
       jne       near ptr M02_L22
       cmp       r14,rdi
       jne       near ptr M02_L23
M02_L01:
       mov       r12,[r12+10]
M02_L02:
       cmp       dword ptr [r12+8],0
       jne       near ptr M02_L24
M02_L03:
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r14],rdi
       jne       near ptr M02_L25
       mov       r13d,[r14+5C]
M02_L04:
       test      r13b,10
       jne       near ptr M02_L26
       test      rbx,rbx
       je        near ptr M02_L27
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       cmp       [rbx],rdi
       jne       near ptr M02_L12
       mov       r13d,26
M02_L05:
       cmp       r13d,17
       je        near ptr M02_L13
       cmp       r13d,37
       je        near ptr M02_L28
M02_L06:
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       cmp       [rbx],rdi
       jne       near ptr M02_L33
       mov       r13,[rbx+10]
M02_L07:
       mov       rdi,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [r15],rdi
       jne       near ptr M02_L34
       mov       r12,[r15+30]
M02_L08:
       test      r12,r12
       je        near ptr M02_L40
       mov       rdi,r12
       mov       rsi,r13
       mov       rax,[r12]
       mov       rax,[rax+0A0]
       call      qword ptr [rax+10]
       test      eax,eax
       je        near ptr M02_L35
M02_L09:
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r14],rdi
       jne       near ptr M02_L41
       mov       rdi,r14
       call      qword ptr [75C4754A6028]; System.Reflection.RuntimeMethodInfo.get_ContainsGenericParameters()
M02_L10:
       test      eax,eax
       jne       near ptr M02_L42
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       lea       rdi,[r14+10]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       mov       rax,r14
       add       rsp,8
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M02_L11:
       mov       rdi,r14
       call      qword ptr [75C475435E60]; System.Runtime.CompilerServices.RuntimeHelpers.<GetHashCode>g__GetHashCodeWorker|15_0(System.Object)
       jmp       near ptr M02_L00
M02_L12:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+20]
       mov       r13d,eax
       jmp       near ptr M02_L05
M02_L13:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rsi],rdi
       jne       near ptr M02_L29
M02_L14:
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rbx],rdi
       jne       short M02_L17
       mov       rsi,[rbx+10]
M02_L15:
       mov       rdi,rsi
       test      rdi,rdi
       je        short M02_L16
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M02_L30
M02_L16:
       test      rdi,rdi
       je        near ptr M02_L06
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M02_L31
       cmp       qword ptr [rdi+18],0
       jne       near ptr M02_L06
       jmp       near ptr M02_L32
M02_L17:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+48]
       call      qword ptr [rax+10]
       mov       rsi,rax
       jmp       short M02_L15
M02_L18:
       mov       edi,31A9
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F258]
       int       3
M02_L19:
       mov       rdi,r15
       mov       esi,1
       mov       rax,[r15]
       mov       rax,[rax+58]
       call      qword ptr [rax+18]
       mov       r14,rax
       test      r14,r14
       jne       short M02_L20
       mov       edi,31A9
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rsi,rax
       mov       rdi,r15
       call      qword ptr [75C476F24A98]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M02_L20:
       mov       rdi,r14
       call      qword ptr [75C47607E2E0]; System.Dynamic.Utils.TypeExtensions.GetParametersCached(System.Reflection.MethodBase)
       cmp       dword ptr [rax+8],1
       je        near ptr M02_L03
       jmp       short M02_L24
M02_L21:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M02_L00
M02_L22:
       mov       rsi,r14
       mov       rax,[rdi]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       test      eax,eax
       jne       near ptr M02_L01
M02_L23:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+50]
       call      qword ptr [rax+18]
       mov       r12,rax
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+50]
       call      qword ptr [rax]
       test      eax,eax
       jne       near ptr M02_L02
       mov       rdi,r13
       mov       rsi,r14
       mov       rdx,r12
       call      qword ptr [75C47607E388]; System.Dynamic.Utils.CacheDict`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].Add(System.__Canon, System.__Canon)
       jmp       near ptr M02_L02
M02_L24:
       mov       edi,31A9
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rsi,rax
       mov       rdi,r14
       call      qword ptr [75C476F24AB0]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M02_L25:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+50]
       call      qword ptr [rax+20]
       mov       r13d,eax
       jmp       near ptr M02_L04
M02_L26:
       test      rbx,rbx
       je        near ptr M02_L09
       mov       edi,3067
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476F24AC8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M02_L27:
       mov       edi,31A9
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476F24AC8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M02_L28:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.IndexExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       cmp       qword ptr [rbx+18],0
       je        near ptr M02_L06
       mov       rdi,[rbx+18]
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M02_L06
       jmp       short M02_L32
M02_L29:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.MemberExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       jmp       near ptr M02_L14
M02_L30:
       mov       rdi,offset MT_System.Reflection.PropertyInfo
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       rdi,rax
       jmp       near ptr M02_L16
M02_L31:
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M02_L06
M02_L32:
       mov       edi,3067
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F3D8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M02_L33:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r13,rax
       jmp       near ptr M02_L07
M02_L34:
       mov       rdi,r15
       mov       rax,[r15]
       mov       rax,[rax+40]
       call      qword ptr [rax+38]
       mov       r12,rax
       jmp       near ptr M02_L08
M02_L35:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       jne       short M02_L36
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       jne       short M02_L36
       mov       rdi,r12
       mov       rsi,r13
       mov       rax,[r12]
       mov       rax,[rax+0B0]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M02_L09
M02_L36:
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       je        near ptr M02_L40
       mov       rdi,r12
       mov       rsi,75C470401D88
       call      qword ptr [75C47607E3E8]; System.Dynamic.Utils.TypeUtils.AreReferenceAssignable(System.Type, System.Type)
       test      eax,eax
       jne       near ptr M02_L09
       mov       rdi,r12
       mov       rsi,75C470405798
       call      qword ptr [75C47607E3E8]; System.Dynamic.Utils.TypeUtils.AreReferenceAssignable(System.Type, System.Type)
       test      eax,eax
       jne       near ptr M02_L09
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+70]
       call      qword ptr [rax+30]
       test      eax,eax
       je        short M02_L37
       mov       rdi,r12
       mov       rsi,75C470409F00
       call      qword ptr [75C47607E3E8]; System.Dynamic.Utils.TypeUtils.AreReferenceAssignable(System.Type, System.Type)
       test      eax,eax
       jne       near ptr M02_L09
M02_L37:
       mov       rdi,r12
       call      qword ptr [75C475D053F8]; System.Type.get_IsInterface()
       test      eax,eax
       je        short M02_L40
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+98]
       call      qword ptr [rax+38]
       mov       r13,rax
       xor       eax,eax
       jmp       short M02_L39
M02_L38:
       mov       [rbp-30],rax
       mov       rsi,[r13+rax*8+10]
       mov       rdi,r12
       call      qword ptr [75C47607E3E8]; System.Dynamic.Utils.TypeUtils.AreReferenceAssignable(System.Type, System.Type)
       test      eax,eax
       jne       near ptr M02_L09
       mov       rdi,[rbp-30]
       inc       edi
       mov       rax,rdi
M02_L39:
       cmp       [r13+8],eax
       jg        short M02_L38
M02_L40:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rbx,rax
       mov       edi,31A9
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdx,rax
       mov       rsi,rbx
       mov       rdi,r15
       call      qword ptr [75C476F24AE0]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M02_L41:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+58]
       call      qword ptr [rax+28]
       jmp       near ptr M02_L10
M02_L42:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+58]
       call      qword ptr [rax+18]
       test      eax,eax
       jne       short M02_L43
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,75C4704064F0
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,r14
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,r15
       mov       rdx,75C4704065B0
       call      qword ptr [75C4764A5CB0]
       jmp       short M02_L44
M02_L43:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,75C470406548
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,r14
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,r15
       mov       rdx,75C4704065B0
       call      qword ptr [75C4764A5CB0]
M02_L44:
       mov       rdi,r15
       call      CORINFO_HELP_THROW
       int       3
M02_L45:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1714
```
```assembly
; System.Object.GetType()
       push      rbx
       mov       rbx,rdi
       mov       rdi,[rbx]
       mov       rax,[rdi+20]
       add       rax,10
       mov       rax,[rax]
       test      rax,rax
       je        short M03_L01
M03_L00:
       pop       rbx
       ret
M03_L01:
       call      qword ptr [75C475045C68]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandleSlow(IntPtr)
       jmp       short M03_L00
; Total bytes of code 33
```
```assembly
; System.RuntimeFieldInfoStub.FromPtr(IntPtr)
       push      rbp
       push      r15
       push      rbx
       lea       rbp,[rsp+10]
       mov       rbx,rdi
       test      rbx,rbx
       je        short M04_L00
       mov       rdi,offset MT_System.RuntimeFieldInfoStub
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,rbx
       call      000075C4F34FA2F0
       lea       rdi,[r15+8]
       mov       rsi,rax
       call      CORINFO_HELP_ASSIGN_REF
       mov       [r15+38],rbx
       mov       rax,r15
       pop       rbx
       pop       r15
       pop       rbp
       ret
M04_L00:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476E8F360]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C476077558]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 112
```
```assembly
; System.Reflection.FieldInfo.GetFieldFromHandle(System.RuntimeFieldHandle)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,48
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-38],rax
       test      rdi,rdi
       je        near ptr M05_L19
       mov       rbx,rdi
       mov       r15,offset MT_System.Reflection.RtFieldInfo
       cmp       [rbx],r15
       jne       near ptr M05_L10
       mov       rdi,[rbx+38]
M05_L00:
       call      000075C4F3501F00
       mov       rdi,[rax+20]
       add       rdi,10
       mov       r14,[rdi]
       test      r14,r14
       je        near ptr M05_L20
M05_L01:
       mov       r13,r14
       mov       rdi,offset MT_System.RuntimeFieldInfoStub
       cmp       [rbx],rdi
       jne       near ptr M05_L21
       mov       r12,[rbx+38]
M05_L02:
       test      r13,r13
       je        near ptr M05_L11
       mov       rdi,r12
       call      000075C4F3501F00
       mov       rdi,[rax+20]
       add       rdi,10
       mov       rcx,[rdi]
       test      rcx,rcx
       je        near ptr M05_L23
M05_L03:
       mov       [rbp-40],rcx
       cmp       r13,rcx
       jne       near ptr M05_L24
M05_L04:
       mov       rdi,[r13+10]
       test      rdi,rdi
       je        near ptr M05_L13
       mov       r14,[rdi]
       test      r14,r14
       je        near ptr M05_L13
M05_L05:
       cmp       [r14],r14b
       lea       r13,[r14+50]
       cmp       qword ptr [r13],0
       je        near ptr M05_L26
M05_L06:
       mov       r14,[r14+50]
       mov       rdi,[r14+8]
       test      rdi,rdi
       je        near ptr M05_L27
       mov       eax,[rdi+8]
       test      eax,eax
       jle       near ptr M05_L27
       add       rdi,10
M05_L07:
       mov       r13,[rdi]
       test      r13,r13
       je        near ptr M05_L27
       cmp       [r13],r15
       jne       near ptr M05_L14
       cmp       [r13+38],r12
       jne       near ptr M05_L14
M05_L08:
       xor       edi,edi
       mov       [rbp-38],rdi
       cmp       [r13],r15
       jne       near ptr M05_L32
       mov       rdi,[r13+8]
       cmp       byte ptr [rdi+9C],0
       jne       near ptr M05_L31
       mov       rbx,[r13+10]
M05_L09:
       test      rbx,rbx
       je        near ptr M05_L18
       mov       rdi,offset MT_System.RuntimeType
       cmp       [rbx],rdi
       jne       near ptr M05_L34
       mov       rdi,[rbx+18]
       test      dil,2
       jne       near ptr M05_L33
       test      dword ptr [rdi],80000000
       jne       short M05_L15
       test      byte ptr [rdi],30
       setne     al
       movzx     eax,al
       jmp       short M05_L16
M05_L10:
       mov       rdi,rbx
       mov       r11,75C4740626C8
       call      qword ptr [r11]
       mov       rdi,rax
       jmp       near ptr M05_L00
M05_L11:
       mov       rdi,r12
       call      000075C4F3501F00
       mov       rdi,[rax+20]
       add       rdi,10
       mov       r13,[rdi]
       test      r13,r13
       je        near ptr M05_L22
M05_L12:
       jmp       near ptr M05_L04
M05_L13:
       mov       rdi,r13
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
       mov       r14,rax
       jmp       near ptr M05_L05
M05_L14:
       add       rdi,8
       dec       eax
       jne       near ptr M05_L07
       jmp       near ptr M05_L27
M05_L15:
       xor       eax,eax
M05_L16:
       movzx     r15d,al
M05_L17:
       test      r15d,r15d
       jne       near ptr M05_L35
M05_L18:
       mov       rax,r13
       add       rsp,48
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M05_L19:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476F24768]
       mov       r15,rax
       mov       edi,13B9A
       mov       rsi,75C474054000
       call      qword ptr [75C47504EF88]
       mov       rdx,rax
       mov       rsi,r15
       mov       rdi,rbx
       call      qword ptr [75C4764A5CB0]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M05_L20:
       mov       rdi,rax
       call      qword ptr [75C475045C68]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandleSlow(IntPtr)
       mov       r14,rax
       jmp       near ptr M05_L01
M05_L21:
       mov       rdi,rbx
       mov       r11,75C4740626D0
       call      qword ptr [r11]
       mov       r12,rax
       jmp       near ptr M05_L02
M05_L22:
       mov       rdi,rax
       call      qword ptr [75C475045C68]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandleSlow(IntPtr)
       mov       r13,rax
       jmp       near ptr M05_L12
M05_L23:
       mov       rdi,rax
       call      qword ptr [75C475045C68]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandleSlow(IntPtr)
       mov       rcx,rax
       jmp       near ptr M05_L03
M05_L24:
       mov       rdi,r12
       call      000075C4F34FA2C0
       test      eax,eax
       je        short M05_L25
       mov       rdi,[rbp-40]
       mov       rsi,r13
       call      000075C4F34FE5B0
       test      eax,eax
       jne       near ptr M05_L04
M05_L25:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r12,rax
       call      qword ptr [75C476F251E8]
       mov       rdi,rax
       mov       rsi,r14
       mov       rdx,[rbp-40]
       call      qword ptr [75C476F24798]
       mov       rsi,rax
       mov       rdi,r12
       call      qword ptr [75C4760769D0]
       mov       rdi,r12
       call      CORINFO_HELP_THROW
       int       3
M05_L26:
       mov       rdi,offset MT_System.RuntimeType+RuntimeTypeCache+MemberInfoCache<System.Reflection.RuntimeFieldInfo>
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-48],rax
       lea       rdi,[rax+10]
       mov       rsi,r14
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdi,r13
       mov       rsi,[rbp-48]
       xor       edx,edx
       call      000075C4F3474060
       jmp       near ptr M05_L06
M05_L27:
       mov       rdi,r12
       call      000075C4F3501E90
       mov       edi,eax
       and       edi,7
       cmp       edi,6
       sete      r13b
       movzx     r13d,r13b
       test      al,10
       setne     al
       movzx     eax,al
       mov       [rbp-2C],eax
       mov       rdi,r12
       call      000075C4F3501F00
       mov       rdi,[rax+20]
       add       rdi,10
       mov       rcx,[rdi]
       test      rcx,rcx
       mov       [rbp-60],rcx
       jne       short M05_L28
       mov       rdi,rax
       call      qword ptr [75C475045C68]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandleSlow(IntPtr)
       mov       [rbp-60],rax
M05_L28:
       mov       rdi,r12
       call      000075C4F34FA2C0
       test      eax,eax
       jne       short M05_L29
       mov       rsi,[r14+10]
       mov       rax,[rbp-60]
       cmp       rax,[rsi+8]
       setne     sil
       movzx     esi,sil
       jmp       short M05_L30
M05_L29:
       mov       rax,[rbp-60]
       mov       rsi,[r14+10]
       mov       rsi,[rsi+8]
       mov       rdi,rax
       call      000075C4F34FE5B0
       test      eax,eax
       sete      sil
       movzx     esi,sil
M05_L30:
       mov       edi,r13d
       mov       edx,[rbp-2C]
       call      qword ptr [75C47543CF30]; System.RuntimeType.FilterPreCalculate(Boolean, Boolean, Boolean)
       mov       r13d,eax
       mov       rdi,offset MT_System.Reflection.RuntimeFieldInfo[]
       mov       esi,1
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       [rbp-50],rax
       mov       rcx,[r14+10]
       mov       [rbp-68],rcx
       mov       rdi,r15
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-58],rax
       mov       rdx,[rbp-68]
       mov       rdx,[rdx+8]
       mov       rcx,[r14+10]
       mov       rdi,rax
       mov       rsi,r12
       mov       r8d,r13d
       call      qword ptr [75C476F26100]
       mov       r13,[rbp-50]
       lea       rdi,[r13+10]
       mov       rsi,[rbp-58]
       call      CORINFO_HELP_ASSIGN_REF
       mov       [rbp-38],r13
       lea       rsi,[rbp-38]
       mov       rdi,r14
       xor       edx,edx
       mov       ecx,3
       call      qword ptr [75C47543CFC0]; System.RuntimeType+RuntimeTypeCache+MemberInfoCache`1[[System.__Canon, System.Private.CoreLib]].Insert(System.__Canon[] ByRef, System.String, MemberListType)
       mov       rdi,[rbp-38]
       cmp       dword ptr [rdi+8],0
       jbe       near ptr M05_L36
       mov       rdi,[rbp-38]
       mov       r13,[rdi+10]
       jmp       near ptr M05_L08
M05_L31:
       xor       ebx,ebx
       jmp       near ptr M05_L09
M05_L32:
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+40]
       call      qword ptr [rax+38]
       mov       rbx,rax
       jmp       near ptr M05_L09
M05_L33:
       xor       eax,eax
       jmp       near ptr M05_L16
M05_L34:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+60]
       call      qword ptr [rax+8]
       mov       r15d,eax
       jmp       near ptr M05_L17
M05_L35:
       call      qword ptr [75C476F251B8]
       mov       r15,rax
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       r13,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+68]
       call      qword ptr [rax+18]
       mov       rdx,rax
       mov       rsi,r13
       mov       rdi,r15
       call      qword ptr [75C476F24798]
       mov       rsi,rax
       mov       rdi,r14
       call      qword ptr [75C4760769D0]
       mov       rdi,r14
       call      CORINFO_HELP_THROW
       int       3
M05_L36:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1230
```
```assembly
; System.Dynamic.Utils.ExpressionUtils.RequiresCanRead(System.Linq.Expressions.Expression, System.String, Int32)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       push      rax
       lea       rbp,[rsp+30]
       mov       rbx,rdi
       mov       r15,rsi
       mov       r14d,edx
       test      rbx,rbx
       je        near ptr M06_L08
       mov       r13,[rbx]
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       cmp       r13,rdi
       jne       short M06_L02
       mov       r12d,26
M06_L00:
       cmp       r12d,17
       je        short M06_L03
       cmp       r12d,37
       je        near ptr M06_L09
M06_L01:
       add       rsp,8
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M06_L02:
       mov       rdi,rbx
       mov       rax,[r13+40]
       call      qword ptr [rax+20]
       mov       r12d,eax
       jmp       short M06_L00
M06_L03:
       mov       rdi,rbx
       mov       rsi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rdi],rsi
       jne       near ptr M06_L10
M06_L04:
       mov       rax,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       r13,rax
       jne       short M06_L07
       mov       rsi,[rdi+10]
M06_L05:
       mov       rdi,rsi
       test      rdi,rdi
       je        short M06_L06
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M06_L11
M06_L06:
       test      rdi,rdi
       je        short M06_L01
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M06_L12
       cmp       qword ptr [rdi+18],0
       jne       near ptr M06_L01
       jmp       near ptr M06_L13
M06_L07:
       mov       rax,[r13+48]
       call      qword ptr [rax+10]
       mov       rsi,rax
       jmp       short M06_L05
M06_L08:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       rdi,r15
       mov       esi,r14d
       call      qword ptr [75C476E8F3C0]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C476077558]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M06_L09:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.IndexExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       cmp       qword ptr [rax+18],0
       je        near ptr M06_L01
       mov       rdi,[rax+18]
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M06_L01
       jmp       short M06_L13
M06_L10:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.MemberExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       rdi,rax
       jmp       near ptr M06_L04
M06_L11:
       mov       rdi,offset MT_System.Reflection.PropertyInfo
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       rdi,rax
       jmp       near ptr M06_L06
M06_L12:
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M06_L01
M06_L13:
       mov       rdi,r15
       mov       esi,r14d
       call      qword ptr [75C476E8F3D8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 416
```
```assembly
; System.Dynamic.Utils.TypeUtils.AreEquivalent(System.Type, System.Type)
       push      rbp
       mov       rbp,rsp
       test      rdi,rdi
       je        short M07_L01
       mov       rax,offset MT_System.RuntimeType
       cmp       [rdi],rax
       jne       short M07_L00
       cmp       rdi,rsi
       sete      al
       movzx     eax,al
       pop       rbp
       ret
M07_L00:
       mov       rax,[rdi]
       mov       rax,[rax+0A0]
       pop       rbp
       jmp       qword ptr [rax+10]
M07_L01:
       xor       eax,eax
       pop       rbp
       ret
; Total bytes of code 54
```
```assembly
; System.Linq.Expressions.Expression.Equal(System.Linq.Expressions.Expression, System.Linq.Expressions.Expression, Boolean, System.Reflection.MethodInfo)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,0C8
       lea       rbp,[rsp+0F0]
       mov       rbx,rdi
       mov       r15,rsi
       mov       r14d,edx
       mov       r13,rcx
       test      rbx,rbx
       je        near ptr M08_L39
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       cmp       [rbx],rdi
       jne       near ptr M08_L10
       mov       r12d,26
M08_L00:
       cmp       r12d,17
       je        near ptr M08_L11
       cmp       r12d,37
       je        near ptr M08_L40
M08_L01:
       test      r15,r15
       je        near ptr M08_L45
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       cmp       [r15],rdi
       jne       near ptr M08_L16
       mov       r12d,26
M08_L02:
       cmp       r12d,17
       je        near ptr M08_L17
       cmp       r12d,37
       je        near ptr M08_L46
M08_L03:
       test      r13,r13
       jne       near ptr M08_L101
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rbx],rdi
       jne       near ptr M08_L53
       mov       r13,[rbx+10]
       mov       rdi,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [r13],rdi
       jne       near ptr M08_L52
       cmp       qword ptr [r13+38],0
       je        near ptr M08_L51
M08_L04:
       mov       rdi,[r13+38]
       mov       r12,[rdi+18]
M08_L05:
       mov       rdi,offset MT_System.Linq.Expressions.FieldExpression
       cmp       [r15],rdi
       jne       near ptr M08_L56
       mov       rdi,[r15+10]
       mov       rax,offset MT_System.Reflection.RtFieldInfo
       cmp       [rdi],rax
       jne       near ptr M08_L55
       mov       r13,[rdi+28]
       test      r13,r13
       je        near ptr M08_L54
M08_L06:
       cmp       r12,r13
       jne       near ptr M08_L57
M08_L07:
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rbx],rdi
       jne       near ptr M08_L60
       mov       r13,[rbx+10]
       mov       rdi,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [r13],rdi
       jne       near ptr M08_L59
       cmp       qword ptr [r13+38],0
       je        near ptr M08_L58
M08_L08:
       mov       rdi,[r13+38]
       mov       r12,[rdi+18]
M08_L09:
       mov       r13,offset MT_System.RuntimeType
       cmp       [r12],r13
       jne       near ptr M08_L62
       mov       rdi,[r12+18]
       test      dil,2
       jne       near ptr M08_L61
       mov       edi,[rdi]
       and       edi,80000030
       cmp       edi,10
       je        near ptr M08_L22
       cmp       edi,20
       sete      al
       movzx     eax,al
       jmp       near ptr M08_L23
M08_L10:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+20]
       mov       r12d,eax
       jmp       near ptr M08_L00
M08_L11:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rsi],rdi
       jne       near ptr M08_L41
M08_L12:
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rbx],rdi
       jne       short M08_L15
       mov       rsi,[rbx+10]
M08_L13:
       mov       rdi,rsi
       test      rdi,rdi
       je        short M08_L14
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M08_L42
M08_L14:
       test      rdi,rdi
       je        near ptr M08_L01
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M08_L43
       cmp       qword ptr [rdi+18],0
       jne       near ptr M08_L01
       jmp       near ptr M08_L44
M08_L15:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+48]
       call      qword ptr [rax+10]
       mov       rsi,rax
       jmp       short M08_L13
M08_L16:
       mov       rdi,r15
       mov       rax,[r15]
       mov       rax,[rax+40]
       call      qword ptr [rax+20]
       mov       r12d,eax
       jmp       near ptr M08_L02
M08_L17:
       mov       rsi,r15
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rsi],rdi
       jne       near ptr M08_L47
M08_L18:
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [r15],rdi
       jne       short M08_L21
       mov       rsi,[r15+10]
M08_L19:
       mov       rdi,rsi
       test      rdi,rdi
       je        short M08_L20
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M08_L48
M08_L20:
       test      rdi,rdi
       je        near ptr M08_L03
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M08_L49
       cmp       qword ptr [rdi+18],0
       jne       near ptr M08_L03
       jmp       near ptr M08_L50
M08_L21:
       mov       rdi,r15
       mov       rax,[r15]
       mov       rax,[rax+48]
       call      qword ptr [rax+10]
       mov       rsi,rax
       jmp       short M08_L19
M08_L22:
       mov       eax,1
M08_L23:
       movzx     eax,al
M08_L24:
       test      eax,eax
       jne       near ptr M08_L63
M08_L25:
       cmp       [r12],r13
       jne       near ptr M08_L65
       mov       rdi,[r12+18]
       test      dil,2
       jne       near ptr M08_L64
       mov       rdi,[rdi+10]
       mov       rsi,offset MT_System.Enum
       cmp       rdi,rsi
       sete      al
       movzx     eax,al
M08_L26:
       test      eax,eax
       jne       near ptr M08_L88
       cmp       [r12],r13
       jne       near ptr M08_L87
       cmp       qword ptr [r12+10],0
       je        short M08_L27
       mov       rdi,[r12+10]
       mov       rax,[rdi]
       test      rax,rax
       jne       near ptr M08_L33
M08_L27:
       mov       rdi,r12
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
M08_L28:
       mov       eax,[rax+98]
       test      eax,eax
       je        near ptr M08_L66
M08_L29:
       add       eax,0FFFFFFFC
       cmp       eax,0A
       ja        near ptr M08_L88
M08_L30:
       mov       rdi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rbx],rdi
       jne       near ptr M08_L91
       mov       r12,[rbx+10]
       mov       rdi,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [r12],rdi
       jne       near ptr M08_L90
       cmp       qword ptr [r12+38],0
       je        near ptr M08_L89
M08_L31:
       mov       rdi,[r12+38]
       mov       r12,[rdi+18]
M08_L32:
       cmp       [r12],r13
       jne       near ptr M08_L93
       mov       rdi,[r12+18]
       test      dil,2
       jne       near ptr M08_L92
       mov       edi,[rdi]
       and       edi,80000030
       cmp       edi,10
       je        short M08_L34
       cmp       edi,20
       sete      al
       movzx     eax,al
       jmp       short M08_L35
M08_L33:
       jmp       near ptr M08_L28
M08_L34:
       mov       eax,1
M08_L35:
       movzx     r13d,al
M08_L36:
       test      r13d,r13d
       jne       near ptr M08_L94
       xor       r13d,r13d
M08_L37:
       movzx     edi,r14b
       test      edi,r13d
       jne       near ptr M08_L95
       mov       rdi,offset MT_System.Linq.Expressions.LogicalBinaryExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       lea       rdi,[r13+10]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       lea       rdi,[r13+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       mov       dword ptr [r13+18],0D
M08_L38:
       mov       rax,r13
       add       rsp,0C8
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M08_L39:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       edi,2BF3
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F3C0]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C476077558]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M08_L40:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.IndexExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       cmp       qword ptr [rbx+18],0
       je        near ptr M08_L01
       mov       rdi,[rbx+18]
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M08_L01
       jmp       short M08_L44
M08_L41:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.MemberExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       jmp       near ptr M08_L12
M08_L42:
       mov       rdi,offset MT_System.Reflection.PropertyInfo
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       rdi,rax
       jmp       near ptr M08_L14
M08_L43:
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M08_L01
M08_L44:
       mov       edi,2BF3
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F3D8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M08_L45:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       edi,2BFD
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F3C0]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C476077558]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M08_L46:
       mov       rsi,r15
       mov       rdi,offset MT_System.Linq.Expressions.IndexExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       cmp       qword ptr [r15+18],0
       je        near ptr M08_L03
       mov       rdi,[r15+18]
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M08_L03
       jmp       short M08_L50
M08_L47:
       mov       rsi,r15
       mov       rdi,offset MT_System.Linq.Expressions.MemberExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       jmp       near ptr M08_L18
M08_L48:
       mov       rdi,offset MT_System.Reflection.PropertyInfo
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       rdi,rax
       jmp       near ptr M08_L20
M08_L49:
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M08_L03
M08_L50:
       mov       edi,2BFD
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F3D8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M08_L51:
       mov       rdi,[r13+30]
       cmp       [rdi],dil
       call      qword ptr [75C476F25620]
       cmp       [rax],al
       xor       edi,edi
       mov       [rbp-58],rdi
       lea       rdi,[rbp-58]
       mov       rsi,rax
       call      qword ptr [75C476F25638]
       mov       rsi,[rbp-58]
       mov       [rbp-30],rsi
       mov       esi,[r13+50]
       lea       rdi,[rbp-30]
       lea       rdx,[rbp-38]
       lea       rcx,[rbp-40]
       lea       r8,[rbp-50]
       call      qword ptr [75C476F25650]
       mov       r12,[rbp-48]
       mov       rdi,offset MT_System.Signature
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-0D0],rax
       mov       rcx,[r13+30]
       mov       rdi,rax
       mov       rsi,r12
       mov       edx,[rbp-50]
       call      qword ptr [75C476E8F180]
       lea       rdi,[r13+38]
       mov       rsi,[rbp-0D0]
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M08_L04
M08_L52:
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+50]
       call      qword ptr [rax+10]
       mov       r12,rax
       jmp       near ptr M08_L05
M08_L53:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r12,rax
       jmp       short M08_L56
M08_L54:
       call      qword ptr [75C476786478]; System.Reflection.RtFieldInfo.InitializeFieldType()
       mov       r13,rax
       jmp       near ptr M08_L06
M08_L55:
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+18]
       mov       r13,rax
       jmp       near ptr M08_L06
M08_L56:
       mov       rdi,r15
       mov       rax,[r15]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r13,rax
       jmp       near ptr M08_L06
M08_L57:
       test      r12,r12
       je        near ptr M08_L96
       test      r13,r13
       je        near ptr M08_L96
       mov       rsi,r12
       mov       rdi,offset MT_System.RuntimeType
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       jne       near ptr M08_L96
       mov       rsi,r13
       mov       rdi,offset MT_System.RuntimeType
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       jne       near ptr M08_L96
       mov       rdi,r12
       mov       rsi,r13
       mov       rax,[r12]
       mov       rax,[rax+0A8]
       call      qword ptr [rax+18]
       test      eax,eax
       je        near ptr M08_L96
       jmp       near ptr M08_L07
M08_L58:
       mov       rdi,[r13+30]
       cmp       [rdi],dil
       call      qword ptr [75C476F25620]
       cmp       [rax],al
       xor       edi,edi
       mov       [rbp-88],rdi
       lea       rdi,[rbp-88]
       mov       rsi,rax
       call      qword ptr [75C476F25638]
       mov       rsi,[rbp-88]
       mov       [rbp-60],rsi
       mov       esi,[r13+50]
       lea       rdi,[rbp-60]
       lea       rdx,[rbp-68]
       lea       rcx,[rbp-70]
       lea       r8,[rbp-80]
       call      qword ptr [75C476F25650]
       mov       r12,[rbp-78]
       mov       rdi,offset MT_System.Signature
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-0D8],rax
       mov       rcx,[r13+30]
       mov       rdi,rax
       mov       rsi,r12
       mov       edx,[rbp-80]
       call      qword ptr [75C476E8F180]
       lea       rdi,[r13+38]
       mov       rsi,[rbp-0D8]
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M08_L08
M08_L59:
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+50]
       call      qword ptr [rax+10]
       mov       r12,rax
       jmp       near ptr M08_L09
M08_L60:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r12,rax
       jmp       near ptr M08_L09
M08_L61:
       xor       eax,eax
       jmp       near ptr M08_L23
M08_L62:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+58]
       call      qword ptr [rax+28]
       jmp       near ptr M08_L24
M08_L63:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+68]
       call      qword ptr [rax+18]
       mov       rdi,75C470406E18
       cmp       rax,rdi
       jne       near ptr M08_L25
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+68]
       call      qword ptr [rax+28]
       cmp       dword ptr [rax+8],0
       jbe       near ptr M08_L102
       mov       r12,[rax+10]
       jmp       near ptr M08_L25
M08_L64:
       mov       rdi,r12
       mov       rsi,75C470409F00
       call      qword ptr [75C47405A510]; Precode of System.RuntimeType.IsSubclassOf(System.Type)
       jmp       near ptr M08_L26
M08_L65:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+70]
       call      qword ptr [rax+30]
       jmp       near ptr M08_L26
M08_L66:
       mov       rax,r12
       mov       [rbp-0E0],rax
       mov       rdi,rax
       call      qword ptr [75C47607E220]; System.RuntimeType.get_IsActualEnum()
       test      eax,eax
       je        short M08_L68
       mov       rdi,r12
       call      qword ptr [75C47405A490]
       mov       rdi,rax
       test      rdi,rdi
       je        short M08_L67
       mov       rcx,offset MT_System.RuntimeType
       cmp       [rdi],rcx
       je        short M08_L67
       mov       rsi,rax
       mov       rdi,rcx
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       int       3
M08_L67:
       mov       [rbp-0E0],rdi
M08_L68:
       mov       rax,75C470406288
       mov       rdi,[rbp-0E0]
       cmp       rdi,rax
       jne       short M08_L69
       mov       eax,5
       jmp       near ptr M08_L84
M08_L69:
       mov       rax,75C4704062B0
       cmp       rdi,rax
       jne       short M08_L70
       mov       eax,6
       jmp       near ptr M08_L84
M08_L70:
       mov       rax,75C4704062D8
       cmp       rdi,rax
       jne       short M08_L71
       mov       eax,7
       jmp       near ptr M08_L84
M08_L71:
       mov       rax,75C470406300
       cmp       rdi,rax
       jne       short M08_L72
       mov       eax,8
       jmp       near ptr M08_L84
M08_L72:
       mov       rax,75C4704021B0
       cmp       rdi,rax
       jne       short M08_L73
       mov       eax,9
       jmp       near ptr M08_L84
M08_L73:
       mov       rax,75C470406328
       cmp       rdi,rax
       jne       short M08_L74
       mov       eax,0A
       jmp       near ptr M08_L84
M08_L74:
       mov       rax,75C470402240
       cmp       rdi,rax
       jne       short M08_L75
       mov       eax,0B
       jmp       near ptr M08_L84
M08_L75:
       mov       rax,75C470406350
       cmp       rdi,rax
       jne       short M08_L76
       mov       eax,0C
       jmp       near ptr M08_L84
M08_L76:
       mov       rax,75C470406378
       cmp       rdi,rax
       jne       short M08_L77
       mov       eax,3
       jmp       near ptr M08_L84
M08_L77:
       mov       rax,75C4704063A0
       cmp       rdi,rax
       jne       short M08_L78
       mov       eax,4
       jmp       near ptr M08_L84
M08_L78:
       mov       rax,75C4704063C8
       cmp       rdi,rax
       jne       short M08_L79
       mov       eax,0D
       jmp       short M08_L84
M08_L79:
       mov       rax,75C4704063F0
       cmp       rdi,rax
       jne       short M08_L80
       mov       eax,0E
       jmp       short M08_L84
M08_L80:
       mov       rax,75C470402ED0
       cmp       rdi,rax
       jne       short M08_L81
       mov       eax,0F
       jmp       short M08_L84
M08_L81:
       mov       rax,75C470406418
       cmp       rdi,rax
       jne       short M08_L82
       mov       eax,10
       jmp       short M08_L84
M08_L82:
       mov       rax,75C470400020
       cmp       rdi,rax
       jne       short M08_L83
       mov       eax,12
       jmp       short M08_L84
M08_L83:
       mov       rax,75C470406440
       mov       ecx,1
       mov       edx,2
       cmp       rdi,rax
       cmove     ecx,edx
       mov       eax,ecx
M08_L84:
       mov       [rbp-8C],eax
       mov       rdi,[r12+10]
       test      rdi,rdi
       je        short M08_L85
       mov       rcx,[rdi]
       test      rcx,rcx
       je        short M08_L85
       mov       rax,rcx
       jmp       short M08_L86
M08_L85:
       mov       rdi,r12
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
M08_L86:
       mov       r12d,[rbp-8C]
       mov       [rax+98],r12d
       mov       eax,r12d
       jmp       near ptr M08_L29
M08_L87:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+98]
       call      qword ptr [rax+10]
       jmp       near ptr M08_L29
M08_L88:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rdi,75C470401D88
       cmp       rax,rdi
       je        near ptr M08_L30
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rdi,rax
       call      qword ptr [75C476F26118]
       test      eax,eax
       jne       near ptr M08_L30
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rdi,rax
       call      qword ptr [75C47607E8F8]; System.Dynamic.Utils.TypeUtils.GetNonNullableType(System.Type)
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+70]
       call      qword ptr [rax+30]
       test      eax,eax
       je        near ptr M08_L96
       jmp       near ptr M08_L30
M08_L89:
       mov       rdi,[r12+30]
       cmp       [rdi],dil
       call      qword ptr [75C476F25620]
       cmp       [rax],al
       xor       edi,edi
       mov       [rbp-0C8],rdi
       lea       rdi,[rbp-0C8]
       mov       rsi,rax
       call      qword ptr [75C476F25638]
       mov       rsi,[rbp-0C8]
       mov       [rbp-98],rsi
       mov       esi,[r12+50]
       lea       rdi,[rbp-98]
       lea       rdx,[rbp-0A0]
       lea       rcx,[rbp-0A8]
       lea       r8,[rbp-0B8]
       call      qword ptr [75C476F25650]
       mov       rax,[rbp-0B0]
       mov       [rbp-0C0],rax
       mov       rdi,offset MT_System.Signature
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-0E8],rax
       mov       rcx,[r12+30]
       mov       rdi,rax
       mov       rsi,[rbp-0C0]
       mov       edx,[rbp-0B8]
       call      qword ptr [75C476E8F180]
       lea       rdi,[r12+38]
       mov       rsi,[rbp-0E8]
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M08_L31
M08_L90:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+50]
       call      qword ptr [rax+10]
       mov       r12,rax
       jmp       near ptr M08_L32
M08_L91:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r12,rax
       jmp       near ptr M08_L32
M08_L92:
       xor       eax,eax
       jmp       near ptr M08_L35
M08_L93:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+58]
       call      qword ptr [rax+28]
       mov       r13d,eax
       jmp       near ptr M08_L36
M08_L94:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+68]
       call      qword ptr [rax+18]
       mov       rdi,75C470406E18
       cmp       rax,rdi
       sete      r13b
       movzx     r13d,r13b
       jmp       near ptr M08_L37
M08_L95:
       mov       rdi,offset MT_System.Linq.Expressions.SimpleBinaryExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rdi,r13
       mov       rcx,r15
       mov       rdx,rbx
       mov       esi,0D
       mov       r8,75C470403068
       call      qword ptr [75C476F25248]
       jmp       near ptr M08_L38
M08_L96:
       movzx     r8d,r14b
       mov       rcx,r15
       mov       rdx,rbx
       mov       edi,0D
       mov       rsi,75C470406648
       call      qword ptr [75C476F25260]
       mov       r13,rax
       test      r13,r13
       je        short M08_L97
       jmp       near ptr M08_L38
M08_L97:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r13,rax
       mov       rdi,r15
       mov       rax,[r15]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rsi,rax
       mov       rdi,r13
       call      qword ptr [75C476F25278]
       test      eax,eax
       jne       short M08_L98
       mov       rdi,rbx
       mov       rsi,r15
       call      qword ptr [75C476F25290]
       test      eax,eax
       je        near ptr M08_L100
M08_L98:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rdi,rax
       call      qword ptr [75C47607E910]; System.Dynamic.Utils.TypeUtils.IsNullableType(System.Type)
       movzx     edi,r14b
       test      edi,eax
       je        short M08_L99
       mov       rdi,offset MT_System.Linq.Expressions.SimpleBinaryExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rdi,r13
       mov       rcx,r15
       mov       rdx,rbx
       mov       esi,0D
       mov       r8,75C470403068
       call      qword ptr [75C476F25248]
       jmp       near ptr M08_L38
M08_L99:
       mov       rdi,offset MT_System.Linq.Expressions.LogicalBinaryExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rdi,r13
       mov       rsi,rbx
       mov       rdx,r15
       call      qword ptr [75C476F26130]
       mov       dword ptr [r13+18],0D
       jmp       near ptr M08_L38
M08_L100:
       mov       rdi,offset MT_System.Linq.Expressions.ExpressionType
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       dword ptr [r14+8],0D
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rbx,rax
       mov       rdi,r15
       mov       rax,[r15]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rdx,rax
       mov       rsi,rbx
       mov       rdi,r14
       call      qword ptr [75C476F252A8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M08_L101:
       movzx     r8d,r14b
       mov       rsi,rbx
       mov       rdx,r15
       mov       rcx,r13
       mov       edi,0D
       call      qword ptr [75C476F25230]
       nop
       add       rsp,0C8
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M08_L102:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 3569
```
```assembly
; System.Linq.Expressions.Expression.Lambda[[System.__Canon, System.Private.CoreLib]](System.Linq.Expressions.Expression, System.String, Boolean, System.Collections.Generic.IEnumerable`1<System.Linq.Expressions.ParameterExpression>)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0D0]
       mov       [rbp-30],rdi
       mov       [rbp-38],rsi
       mov       [rbp-60],rdx
       mov       r15,rdi
       mov       r13d,ecx
       mov       rbx,r8
       test      rbx,rbx
       je        near ptr M09_L78
       mov       rsi,75BE85000EE0
       cmp       rbx,[rsi]
       je        near ptr M09_L78
       mov       rsi,offset MT_System.Runtime.CompilerServices.TrueReadOnlyCollection<System.Linq.Expressions.ParameterExpression>
       cmp       [rbx],rsi
       je        near ptr M09_L13
       mov       rsi,offset MT_System.Runtime.CompilerServices.ReadOnlyCollectionBuilder<System.Linq.Expressions.ParameterExpression>
       cmp       [rbx],rsi
       je        near ptr M09_L12
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Enumerable+Iterator<System.Linq.Expressions.ParameterExpression>
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       jne       near ptr M09_L67
       mov       rsi,rbx
       mov       rdi,offset MT_System.Collections.Generic.ICollection<System.Linq.Expressions.ParameterExpression>
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       r12,rax
       test      r12,r12
       je        near ptr M09_L77
       mov       rbx,[r12]
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       cmp       rbx,rdi
       jne       near ptr M09_L68
       mov       eax,[r12+8]
M09_L00:
       test      eax,eax
       je        near ptr M09_L76
       movsxd    rsi,eax
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       [rbp-70],rax
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       cmp       rbx,rdi
       jne       near ptr M09_L75
       mov       r8d,[r12+8]
       cmp       dword ptr [rbx+4],18
       jne       near ptr M09_L74
       cmp       r8d,[r12+8]
       ja        near ptr M09_L74
       cmp       r8d,[rax+8]
       ja        near ptr M09_L74
       mov       edx,r8d
       movzx     edi,word ptr [MT_System.Linq.Expressions.ParameterExpression[]]
       imul      rdx,rdi
       lea       rsi,[r12+10]
       lea       rdi,[rax+10]
       test      dword ptr [rbx],1000000
       jne       near ptr M09_L69
       mov       rax,[rbp-70]
       mov       rcx,rdi
       mov       r8,rsi
       mov       r9,rdx
       mov       r10,rcx
       sub       r10,r8
       cmp       r10,r9
       jb        near ptr M09_L72
       mov       r10,r8
       sub       r10,rcx
       cmp       r10,r9
       jb        near ptr M09_L72
       lea       r10,[r8+r9]
       lea       r11,[rcx+r9]
       cmp       r9,10
       ja        short M09_L03
       test      dl,18
       je        short M09_L01
       mov       rdx,[rsi]
       mov       [rdi],rdx
       mov       rsi,[r10-8]
       mov       [r11-8],rsi
       jmp       short M09_L06
M09_L01:
       test      dl,4
       je        short M09_L02
       mov       edx,[rsi]
       mov       [rdi],edx
       mov       esi,[r10-4]
       mov       [r11-4],esi
       jmp       short M09_L06
M09_L02:
       test      rdx,rdx
       je        short M09_L06
       movzx     r9d,byte ptr [rsi]
       mov       [rdi],r9b
       test      dl,2
       je        short M09_L06
       movsx     rdi,word ptr [r10-2]
       mov       [r11-2],di
       jmp       short M09_L06
M09_L03:
       cmp       r9,40
       ja        short M09_L08
M09_L04:
       vmovups   xmm0,[r8]
       vmovups   [rcx],xmm0
       cmp       r9,20
       ja        near ptr M09_L11
M09_L05:
       vmovups   xmm0,[r10-10]
       vmovups   [r11-10],xmm0
M09_L06:
       mov       rbx,rax
M09_L07:
       cmp       dword ptr [rbx+8],0
       je        near ptr M09_L78
       mov       rdi,offset MT_System.Runtime.CompilerServices.TrueReadOnlyCollection<System.Linq.Expressions.ParameterExpression>
       call      CORINFO_HELP_NEWSFAST
       mov       r12,rax
       lea       rdi,[r12+8]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M09_L14
M09_L08:
       cmp       r9,800
       ja        near ptr M09_L71
       cmp       r9,100
       jae       near ptr M09_L70
M09_L09:
       mov       rdi,r9
       shr       rdi,6
M09_L10:
       vmovdqu   ymm0,ymmword ptr [r8]
       vmovdqu   ymmword ptr [rcx],ymm0
       vmovdqu   ymm0,ymmword ptr [r8+20]
       vmovdqu   ymmword ptr [rcx+20],ymm0
       add       rcx,40
       add       r8,40
       dec       rdi
       jne       short M09_L10
       and       r9,3F
       cmp       r9,10
       ja        near ptr M09_L04
       jmp       near ptr M09_L05
M09_L11:
       vmovups   xmm0,[r8+10]
       vmovups   [rcx+10],xmm0
       cmp       r9,30
       jbe       near ptr M09_L05
       vmovups   xmm0,[r8+20]
       vmovups   [rcx+20],xmm0
       jmp       near ptr M09_L05
M09_L12:
       mov       rdi,rbx
       call      qword ptr [75C476E8FC00]
       mov       r12,rax
       jmp       short M09_L14
M09_L13:
       mov       r12,rbx
M09_L14:
       mov       [rbp-68],r12
       mov       rbx,[rbp-38]
       test      rbx,rbx
       je        near ptr M09_L79
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       cmp       [rbx],rdi
       jne       short M09_L18
       mov       eax,26
M09_L15:
       cmp       eax,17
       je        short M09_L19
       cmp       eax,37
       je        near ptr M09_L80
M09_L16:
       mov       rdi,[r15+18]
       mov       rdi,[rdi]
       mov       rax,offset MT_System.MulticastDelegate
       cmp       rdi,rax
       je        near ptr M09_L28
       call      System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       rbx,rax
       test      rbx,rbx
       jne       near ptr M09_L24
M09_L17:
       mov       edi,30C1
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476F24C90]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M09_L18:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+20]
       jmp       short M09_L15
M09_L19:
       mov       rdi,rbx
       mov       rsi,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rdi],rsi
       jne       near ptr M09_L81
M09_L20:
       mov       rax,offset MT_System.Linq.Expressions.PropertyExpression
       cmp       [rdi],rax
       jne       short M09_L23
       mov       rsi,[rdi+10]
M09_L21:
       mov       rdi,rsi
       test      rdi,rdi
       je        short M09_L22
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M09_L82
M09_L22:
       test      rdi,rdi
       je        near ptr M09_L16
       mov       rax,offset MT_System.Reflection.RuntimePropertyInfo
       cmp       [rdi],rax
       jne       near ptr M09_L83
       cmp       qword ptr [rdi+18],0
       jne       near ptr M09_L16
       jmp       near ptr M09_L84
M09_L23:
       mov       rax,[rdi]
       mov       rax,[rax+48]
       call      qword ptr [rax+10]
       mov       rsi,rax
       jmp       short M09_L21
M09_L24:
       mov       rdi,offset MT_System.RuntimeType
       cmp       [rbx],rdi
       jne       near ptr M09_L17
       mov       rdi,[rbx+18]
       mov       rsi,offset MT_System.MulticastDelegate
       cmp       rdi,rsi
       je        near ptr M09_L85
       mov       rsi,75BE85000038
       mov       rsi,[rsi]
       add       rsi,10
       rorx      rdx,rdi,20
       mov       rax,offset MT_System.MulticastDelegate
       xor       rdx,rax
       mov       rax,9E3779B97F4A7C15
       imul      rdx,rax
       mov       eax,[rsi]
       shrx      rdx,rdx,rax
       xor       eax,eax
M09_L25:
       lea       ecx,[rdx+1]
       movsxd    rcx,ecx
       lea       rcx,[rcx+rcx*2]
       lea       rcx,[rsi+rcx*8]
       mov       r8d,[rcx]
       mov       r9,[rcx+8]
       and       r8d,0FFFFFFFE
       cmp       r9,rdi
       jne       near ptr M09_L50
       mov       r9,offset MT_System.MulticastDelegate
       xor       r9,[rcx+10]
       cmp       r9,1
       ja        near ptr M09_L50
       cmp       r8d,[rcx]
       jne       near ptr M09_L86
M09_L26:
       test      r9d,r9d
       jne       near ptr M09_L51
       xor       eax,eax
M09_L27:
       mov       rdi,75C4704067B0
       movzx     edi,al
       test      edi,edi
       je        near ptr M09_L17
M09_L28:
       mov       rdi,[r15+18]
       mov       rdi,[rdi]
       mov       rax,offset MT_System.MulticastDelegate
       cmp       rdi,rax
       je        near ptr M09_L17
       call      System.RuntimeTypeHandle.GetRuntimeTypeFromHandle(IntPtr)
       mov       rbx,rax
       mov       [rbp-0C8],rbx
       mov       rdi,rbx
M09_L29:
       mov       rax,[rdi]
       mov       [rbp-58],rax
       mov       rcx,offset MT_System.RuntimeType
       cmp       rax,rcx
       jne       near ptr M09_L90
       mov       [rbp-0B0],rdi
       mov       rcx,[rdi+18]
       mov       rdi,rcx
       mov       rcx,75C4F3474CD0
       vzeroupper
       call      rcx
       movzx     ebx,al
       mov       r14,[rbp-0B0]
       cmp       dword ptr [75C4F36CEC80],0
       jne       near ptr M09_L88
M09_L30:
       cmp       ebx,1D
       ja        short M09_L31
       mov       edi,1FEF7FFF
       bt        edi,ebx
       jae       near ptr M09_L89
M09_L31:
       cmp       ebx,10
       sete      r12b
       movzx     r12d,r12b
M09_L32:
       test      r12d,r12d
       jne       near ptr M09_L87
       mov       rdi,offset MT_System.RuntimeType
       mov       r12,[rbp-58]
       cmp       r12,rdi
       jne       near ptr M09_L91
       mov       rdi,r14
M09_L33:
       test      rdi,rdi
       je        near ptr M09_L92
       call      000075C4F34FE6D0
       test      eax,eax
       jne       near ptr M09_L93
       mov       rdi,75BE85000EF0
       mov       r14,[rdi]
       mov       rbx,[rbp-0C8]
       mov       r12,rbx
       mov       rdi,offset MT_System.RuntimeType
       cmp       [r12],rdi
       jne       near ptr M09_L96
       mov       rdi,r12
       call      000075C4F32E52F0
       test      eax,eax
       je        near ptr M09_L53
M09_L34:
       mov       rdi,[r14+8]
       mov       esi,eax
       and       esi,[r14+10]
       cmp       esi,[rdi+8]
       jae       near ptr M09_L122
       mov       rcx,[rdi+rsi*8+10]
       mov       [rbp-0B8],rcx
       test      rcx,rcx
       je        near ptr M09_L98
       cmp       [rcx+18],eax
       jne       near ptr M09_L98
       mov       rdi,[rcx+8]
       mov       rsi,offset MT_System.RuntimeType
       cmp       [rdi],rsi
       jne       near ptr M09_L97
       cmp       r12,rdi
       jne       near ptr M09_L98
M09_L35:
       mov       rcx,[rbp-0B8]
       mov       r12,[rcx+10]
M09_L36:
       mov       rdi,75BE85000EC8
       mov       rbx,[rdi]
       mov       rdi,offset MT_System.RuntimeType
       cmp       [r12],rdi
       jne       near ptr M09_L99
       mov       rdi,r12
       call      000075C4F32E52F0
       test      eax,eax
       je        near ptr M09_L54
M09_L37:
       mov       rdi,[rbx+8]
       mov       esi,eax
       and       esi,[rbx+10]
       cmp       esi,[rdi+8]
       jae       near ptr M09_L122
       mov       r14,[rdi+rsi*8+10]
       test      r14,r14
       je        near ptr M09_L101
       cmp       [r14+18],eax
       jne       near ptr M09_L101
       mov       rdi,[r14+8]
       mov       rsi,offset MT_System.RuntimeType
       cmp       [rdi],rsi
       jne       near ptr M09_L100
       cmp       r12,rdi
       jne       near ptr M09_L101
M09_L38:
       mov       r14,[r14+10]
M09_L39:
       mov       ebx,[r14+8]
       test      ebx,ebx
       je        near ptr M09_L115
       mov       [rbp-3C],ebx
       mov       rcx,[rbp-68]
       mov       rdi,[rcx+8]
       mov       r11,offset MT_System.Linq.Expressions.ParameterExpression[]
       cmp       [rdi],r11
       jne       near ptr M09_L55
       mov       edx,[rdi+8]
M09_L40:
       mov       eax,[rbp-3C]
       cmp       eax,edx
       jne       near ptr M09_L116
       mov       rdi,offset MT_System.Collections.Generic.HashSet<System.Linq.Expressions.ParameterExpression>
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-78],rax
       mov       rdi,75BE85000F08
       mov       rsi,[rdi]
       lea       rdi,[rax+18]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       mov       [rbp-80],rax
       xor       ecx,ecx
M09_L41:
       cmp       ecx,ebx
       jl        near ptr M09_L56
       mov       rbx,[rbp-68]
M09_L42:
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r12],rdi
       jne       near ptr M09_L117
       mov       rax,[r12+30]
       test      rax,rax
       jne       short M09_L43
       mov       rdi,r12
       call      qword ptr [75C47543D488]; System.Reflection.RuntimeMethodInfo.<get_Signature>g__LazyCreateSignature|25_0()
M09_L43:
       mov       r14,[rax+18]
M09_L44:
       mov       rdi,75C470401840
       cmp       r14,rdi
       je        short M09_L48
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r12],rdi
       jne       near ptr M09_L118
       mov       rax,[r12+30]
       test      rax,rax
       jne       short M09_L45
       mov       rdi,r12
       call      qword ptr [75C47543D488]; System.Reflection.RuntimeMethodInfo.<get_Signature>g__LazyCreateSignature|25_0()
M09_L45:
       mov       r14,[rax+18]
M09_L46:
       mov       rdi,[rbp-38]
       mov       rax,[rdi]
       mov       rcx,offset MT_System.Linq.Expressions.LogicalBinaryExpression
       cmp       rax,rcx
       jne       near ptr M09_L65
       mov       rcx,75C470406378
M09_L47:
       test      r14,r14
       je        near ptr M09_L119
       mov       rdi,r14
       mov       [rbp-0A8],rcx
       mov       rsi,rcx
       mov       rax,[r14]
       mov       rax,[rax+0A0]
       call      qword ptr [rax+10]
       test      eax,eax
       je        near ptr M09_L120
M09_L48:
       mov       rdi,[r15+18]
       mov       rdi,[rdi+20]
       test      rdi,rdi
       je        near ptr M09_L66
M09_L49:
       movzx     ecx,r13b
       mov       rsi,[rbp-38]
       mov       rdx,[rbp-60]
       mov       r8,rbx
       call      qword ptr [75C47607EB80]; System.Linq.Expressions.Expression`1[[System.__Canon, System.Private.CoreLib]].Create(System.Linq.Expressions.Expression, System.String, Boolean, System.Collections.Generic.IReadOnlyList`1<System.Linq.Expressions.ParameterExpression>)
       nop
       vzeroupper
       add       rsp,0A8
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M09_L50:
       test      r8d,r8d
       je        near ptr M09_L86
       inc       eax
       add       edx,eax
       and       edx,[rsi+4]
       cmp       eax,8
       jl        near ptr M09_L25
       jmp       near ptr M09_L86
M09_L51:
       cmp       r9d,1
       je        short M09_L52
       mov       rsi,offset MT_System.MulticastDelegate
       mov       edx,1
       call      qword ptr [75C4754349C0]; System.Runtime.CompilerServices.TypeHandle.CanCastToWorker(System.Runtime.CompilerServices.TypeHandle, System.Runtime.CompilerServices.TypeHandle, Boolean)
       jmp       near ptr M09_L27
M09_L52:
       mov       eax,1
       jmp       near ptr M09_L27
M09_L53:
       mov       rdi,r12
       call      qword ptr [75C475435E60]; System.Runtime.CompilerServices.RuntimeHelpers.<GetHashCode>g__GetHashCodeWorker|15_0(System.Object)
       jmp       near ptr M09_L34
M09_L54:
       mov       rdi,r12
       call      qword ptr [75C475435E60]; System.Runtime.CompilerServices.RuntimeHelpers.<GetHashCode>g__GetHashCodeWorker|15_0(System.Object)
       jmp       near ptr M09_L37
M09_L55:
       mov       r11,75C474062E38
       call      qword ptr [r11]
       mov       edx,eax
       jmp       near ptr M09_L40
M09_L56:
       mov       rdx,[rbp-68]
       mov       rdi,[rdx+8]
       mov       rsi,offset MT_System.Dynamic.Utils.ListParameterProvider
       cmp       [rdi],rsi
       jne       near ptr M09_L63
       test      ecx,ecx
       jne       near ptr M09_L102
       mov       r8,[rdi+10]
M09_L57:
       mov       edi,[r14+8]
       cmp       ecx,edi
       jae       near ptr M09_L122
       mov       [rbp-40],ecx
       mov       edi,ecx
       mov       r9,[r14+rdi*8+10]
       mov       [rbp-88],r9
       mov       [rbp-0C0],r8
       test      r8,r8
       je        near ptr M09_L112
       mov       rdi,offset MT_System.Reflection.RuntimeParameterInfo
       cmp       [r9],rdi
       jne       near ptr M09_L106
       cmp       qword ptr [r9+8],0
       je        near ptr M09_L103
M09_L58:
       mov       rsi,[r9+8]
M09_L59:
       mov       [rbp-98],rsi
       mov       [rbp-90],rsi
       mov       r8,[rbp-0C0]
       mov       r10,[r8]
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression
       cmp       r10,rdi
       jne       near ptr M09_L64
       mov       r9,[rbp-90]
M09_L60:
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       cmp       r10,rdi
       jne       near ptr M09_L108
       mov       rsi,[r8+10]
M09_L61:
       test      rsi,rsi
       mov       [rbp-0C0],r8
       je        near ptr M09_L109
       mov       [rbp-0A0],rsi
       mov       rdi,rsi
       mov       [rbp-90],r9
       mov       rsi,r9
       mov       r10,[rbp-0A0]
       mov       r11,[r10]
       mov       r11,[r11+0A0]
       call      qword ptr [r11+10]
       test      eax,eax
       mov       rsi,[rbp-0A0]
       je        near ptr M09_L110
M09_L62:
       lea       rdx,[rbp-48]
       mov       rdi,[rbp-80]
       mov       rsi,[rbp-0C0]
       call      qword ptr [75C475D04F60]; System.Collections.Generic.HashSet`1[[System.__Canon, System.Private.CoreLib]].AddIfNotPresent(System.__Canon, Int32 ByRef)
       test      eax,eax
       je        near ptr M09_L114
       mov       ecx,[rbp-40]
       inc       ecx
       mov       [rbp-40],ecx
       mov       ecx,[rbp-40]
       jmp       near ptr M09_L41
M09_L63:
       mov       [rbp-40],ecx
       mov       esi,ecx
       mov       r11,75C474062E40
       call      qword ptr [r11]
       mov       r8,rax
       mov       ecx,[rbp-40]
       jmp       near ptr M09_L57
M09_L64:
       mov       [rbp-0C0],r8
       mov       rdi,r8
       mov       [rbp-50],r10
       mov       r11,[r10+48]
       call      qword ptr [r11+10]
       test      eax,eax
       mov       r9,[rbp-90]
       jne       near ptr M09_L107
       mov       r8,[rbp-0C0]
       mov       r10,[rbp-50]
       jmp       near ptr M09_L60
M09_L65:
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       rcx,rax
       jmp       near ptr M09_L47
M09_L66:
       mov       rdi,r15
       mov       rsi,75C476F32948
       call      qword ptr [75C47504F3A8]; System.Runtime.CompilerServices.GenericsHelpers.Method(IntPtr, IntPtr)
       mov       rdi,rax
       jmp       near ptr M09_L49
M09_L67:
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+48]
       call      qword ptr [rax+30]
       mov       rbx,rax
       jmp       near ptr M09_L07
M09_L68:
       mov       rdi,r12
       mov       r11,75C474062E28
       call      qword ptr [r11]
       jmp       near ptr M09_L00
M09_L69:
       call      qword ptr [75C475045788]; System.Buffer.BulkMoveWithWriteBarrier(Byte ByRef, Byte ByRef, UIntPtr)
       mov       rax,[rbp-70]
       jmp       near ptr M09_L06
M09_L70:
       mov       r8,rdi
       and       r8,3F
       mov       r9,r8
       neg       r9
       add       r9,40
       vmovdqu   ymm0,ymmword ptr [rsi]
       vmovdqu   ymmword ptr [rdi],ymm0
       vmovdqu   ymm0,ymmword ptr [rsi+20]
       vmovdqu   ymmword ptr [rdi+20],ymm0
       lea       r8,[rsi+r9]
       lea       rcx,[rdi+r9]
       sub       rdx,r9
       mov       r9,rdx
       jmp       near ptr M09_L09
M09_L71:
       jmp       short M09_L73
M09_L72:
       cmp       rdi,rsi
       je        near ptr M09_L06
M09_L73:
       cmp       [rdi],dil
       call      qword ptr [75C4750466D0]; System.Buffer.MemmoveInternal(Byte ByRef, Byte ByRef, UIntPtr)
       mov       rax,[rbp-70]
       jmp       near ptr M09_L06
M09_L74:
       mov       rdi,r12
       mov       rdx,rax
       xor       esi,esi
       xor       ecx,ecx
       xor       r9d,r9d
       call      qword ptr [75C476E8FCF0]; System.Array.CopyImpl(System.Array, Int32, System.Array, Int32, Int32, Boolean)
       mov       rax,[rbp-70]
       jmp       near ptr M09_L06
M09_L75:
       mov       rdi,r12
       mov       rsi,rax
       mov       r11,75C474062E30
       xor       edx,edx
       call      qword ptr [r11]
       mov       rax,[rbp-70]
       jmp       near ptr M09_L06
M09_L76:
       mov       rsi,75BE85000EE8
       mov       rbx,[rsi]
       jmp       near ptr M09_L07
M09_L77:
       mov       rsi,rbx
       mov       rdi,75C476F44080
       call      qword ptr [75C476E8FBD0]
       mov       rbx,rax
       jmp       near ptr M09_L07
M09_L78:
       mov       rdi,75BE85000EE0
       mov       r12,[rdi]
       jmp       near ptr M09_L14
M09_L79:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       edi,2EFB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F3C0]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C476077558]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M09_L80:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.IndexExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       cmp       qword ptr [rax+18],0
       je        near ptr M09_L16
       mov       rdi,[rax+18]
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M09_L16
       jmp       short M09_L84
M09_L81:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Linq.Expressions.MemberExpression
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       rdi,rax
       jmp       near ptr M09_L20
M09_L82:
       mov       rdi,offset MT_System.Reflection.PropertyInfo
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       rdi,rax
       jmp       near ptr M09_L22
M09_L83:
       mov       rax,[rdi]
       mov       rax,[rax+50]
       call      qword ptr [rax+28]
       test      eax,eax
       jne       near ptr M09_L16
M09_L84:
       mov       edi,2EFB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F3D8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M09_L85:
       mov       r9d,1
       jmp       near ptr M09_L26
M09_L86:
       mov       r9d,2
       jmp       near ptr M09_L26
M09_L87:
       mov       rdi,r14
       mov       r14,[rbp-58]
       mov       rax,[r14+68]
       call      qword ptr [rax+8]
       mov       r14,rax
       mov       rdi,r14
       jmp       near ptr M09_L29
M09_L88:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M09_L30
M09_L89:
       mov       r12d,1
       jmp       near ptr M09_L32
M09_L90:
       mov       [rbp-0B0],rdi
       mov       rax,[rbp-58]
       mov       rcx,[rax+68]
       call      qword ptr [rcx]
       mov       r12d,eax
       mov       r14,[rbp-0B0]
       jmp       near ptr M09_L32
M09_L91:
       mov       rdi,r14
       mov       rax,[r12+98]
       call      qword ptr [rax+8]
       mov       rdi,rax
       jmp       near ptr M09_L33
M09_L92:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476E8F360]
       mov       rdx,rax
       mov       rdi,rbx
       xor       esi,esi
       call      qword ptr [75C476E8F378]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M09_L93:
       mov       rbx,[rbp-0C8]
       mov       rdi,rbx
       call      qword ptr [75C47405A288]; Precode of System.RuntimeType.get_IsGenericTypeDefinition()
       test      eax,eax
       jne       short M09_L94
       mov       rdi,75C470406708
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F738]
       mov       r15,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rdi,75C470406208
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,rbx
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,r14
       mov       rdx,r15
       call      qword ptr [75C4764A5CB0]
       jmp       short M09_L95
M09_L94:
       mov       r14,rbx
       mov       rdi,75C470406708
       mov       esi,0FFFFFFFF
       call      qword ptr [75C476E8F738]
       mov       rbx,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,75C470406258
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,r14
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,r15
       mov       rdx,rbx
       call      qword ptr [75C4764A5CB0]
       mov       r14,r15
M09_L95:
       mov       rdi,r14
       call      CORINFO_HELP_THROW
       int       3
M09_L96:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M09_L34
M09_L97:
       mov       rsi,r12
       mov       rax,[rdi]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       test      eax,eax
       jne       near ptr M09_L35
M09_L98:
       mov       rdi,rbx
       mov       rsi,75C470406788
       mov       edx,34
       call      qword ptr [75C474058490]; Precode of System.Type.GetMethod(System.String, System.Reflection.BindingFlags)
       mov       r12,rax
       mov       rdi,rbx
       call      qword ptr [75C47405A1F8]; Precode of System.RuntimeType.get_IsCollectible()
       test      eax,eax
       jne       near ptr M09_L36
       mov       rsi,rbx
       mov       rdi,r14
       mov       rdx,r12
       call      qword ptr [75C47607E388]; System.Dynamic.Utils.CacheDict`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].Add(System.__Canon, System.__Canon)
       jmp       near ptr M09_L36
M09_L99:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M09_L37
M09_L100:
       mov       rsi,r12
       mov       rax,[rdi]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       test      eax,eax
       jne       near ptr M09_L38
M09_L101:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+50]
       call      qword ptr [rax+18]
       mov       r14,rax
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+50]
       call      qword ptr [rax]
       test      eax,eax
       jne       near ptr M09_L39
       mov       rdi,rbx
       mov       rsi,r12
       mov       rdx,r14
       call      qword ptr [75C47607E388]; System.Dynamic.Utils.CacheDict`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].Add(System.__Canon, System.__Canon)
       jmp       near ptr M09_L39
M09_L102:
       mov       rdi,[rdi+8]
       mov       [rbp-40],ecx
       mov       esi,ecx
       mov       r11,75C474062E48
       call      qword ptr [r11]
       mov       r8,rax
       mov       ecx,[rbp-40]
       jmp       near ptr M09_L57
M09_L103:
       cmp       dword ptr [r9+2C],0FFFFFFFF
       jne       short M09_L104
       mov       rdi,[r9+30]
       mov       rsi,[rdi+18]
       jmp       short M09_L105
M09_L104:
       mov       rsi,[r9+30]
       mov       rdi,[rsi+8]
       mov       esi,[r9+2C]
       cmp       esi,[rdi+8]
       jae       near ptr M09_L122
       mov       rsi,[rdi+rsi*8+10]
M09_L105:
       lea       rdi,[r9+8]
       call      CORINFO_HELP_ASSIGN_REF
       mov       r9,[rbp-88]
       jmp       near ptr M09_L58
M09_L106:
       mov       rdi,r9
       mov       rsi,[r9]
       mov       rsi,[rsi+40]
       call      qword ptr [rsi+38]
       mov       rsi,rax
       jmp       near ptr M09_L59
M09_L107:
       mov       rdi,[rbp-98]
       mov       [rbp-98],rdi
       mov       rax,[rdi]
       mov       rax,[rax+58]
       call      qword ptr [rax+18]
       test      eax,eax
       je        near ptr M09_L113
       mov       rdi,[rbp-98]
       mov       rax,[rdi]
       mov       rax,[rax+68]
       call      qword ptr [rax+8]
       mov       [rbp-90],rax
       mov       r8,[rbp-0C0]
       mov       r9,[rbp-90]
       mov       r10,[rbp-50]
       jmp       near ptr M09_L60
M09_L108:
       mov       [rbp-90],r9
       mov       [rbp-0C0],r8
       mov       rdi,r8
       mov       rsi,[r10+40]
       call      qword ptr [rsi+28]
       mov       rsi,rax
       mov       r8,[rbp-0C0]
       mov       r9,[rbp-90]
       jmp       near ptr M09_L61
M09_L109:
       mov       [rbp-90],r9
M09_L110:
       mov       rdi,rsi
       mov       [rbp-0A0],rsi
       mov       r10,[rsi]
       mov       r10,[r10+78]
       call      qword ptr [r10+8]
       test      eax,eax
       jne       short M09_L111
       mov       rdi,[rbp-90]
       mov       [rbp-90],rdi
       mov       rax,[rdi]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       jne       short M09_L111
       mov       rdi,[rbp-0A0]
       mov       rsi,[rbp-90]
       mov       rax,[rdi]
       mov       rax,[rax+0B0]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M09_L62
M09_L111:
       mov       rdi,[rbp-0C0]
       mov       r10,[rdi]
       mov       r12,r10
       mov       rax,[r12+40]
       call      qword ptr [rax+28]
       mov       rdi,rax
       mov       rsi,[rbp-90]
       call      qword ptr [75C476F24CA8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M09_L112:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       edi,30E9
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       mov       esi,[rbp-40]
       call      qword ptr [75C476E8F3C0]
       mov       rsi,rax
       mov       rdi,r15
       call      qword ptr [75C476077558]
       mov       rdi,r15
       call      CORINFO_HELP_THROW
       int       3
M09_L113:
       mov       rdi,[rbp-0C0]
       mov       r12,[rbp-50]
       mov       rax,[r12+40]
       call      qword ptr [rax+28]
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+0A8]
       call      qword ptr [rax]
       mov       rdi,rax
       mov       rsi,[rbp-98]
       call      qword ptr [75C476F24CA8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M09_L114:
       mov       edi,30E9
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rsi,rax
       mov       rdi,[rbp-0C0]
       mov       edx,[rbp-40]
       call      qword ptr [75C476F24CC0]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M09_L115:
       mov       rbx,[rbp-68]
       mov       rdi,[rbx+8]
       mov       r11,75C474062E50
       call      qword ptr [r11]
       test      eax,eax
       jle       near ptr M09_L42
M09_L116:
       call      qword ptr [75C476F24CD8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M09_L117:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+60]
       call      qword ptr [rax+28]
       mov       r14,rax
       jmp       near ptr M09_L44
M09_L118:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+60]
       call      qword ptr [rax+28]
       mov       r14,rax
       jmp       near ptr M09_L46
M09_L119:
       mov       [rbp-0A8],rcx
M09_L120:
       mov       rdi,r14
       mov       rax,[r14]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       jne       short M09_L121
       mov       rdi,[rbp-0A8]
       mov       [rbp-0A8],rdi
       mov       rax,[rdi]
       mov       rax,[rax+78]
       call      qword ptr [rax+8]
       test      eax,eax
       jne       short M09_L121
       mov       rdi,r14
       mov       rsi,[rbp-0A8]
       mov       rax,[r14]
       mov       rax,[rax+0B0]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M09_L48
M09_L121:
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+60]
       call      qword ptr [rax+28]
       mov       rdi,rax
       lea       rsi,[rbp-38]
       call      qword ptr [75C476F24CF0]
       test      eax,eax
       jne       near ptr M09_L48
       mov       rdi,[rbp-38]
       mov       rax,[rbp-38]
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+28]
       mov       r15,rax
       mov       rdi,r12
       mov       rax,[r12]
       mov       rax,[rax+60]
       call      qword ptr [rax+28]
       mov       rsi,rax
       mov       rdi,r15
       call      qword ptr [75C476F24D08]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M09_L122:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 4451
```
```assembly
; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].Where(System.Linq.Expressions.Expression`1<System.Func`2<System.__Canon,Boolean>>)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,18
       lea       rbp,[rsp+40]
       mov       rbx,rdi
       mov       r15,rsi
       mov       r14,[rbx]
       mov       rdi,offset MT_NextORM.Core.EntityBuilder<NextORM.Benchmark.SimpleEntity>
       cmp       r14,rdi
       jne       near ptr M10_L16
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       r12,[rbx+8]
       mov       rdi,75BE850004E0
       mov       rsi,[rdi]
       lea       rdi,[r13+20]
       call      CORINFO_HELP_ASSIGN_REF
       lea       rdi,[r13+8]
       mov       rsi,r12
       call      CORINFO_HELP_ASSIGN_REF
       mov       rsi,[rbx+0F0]
       lea       rdi,[r13+0F0]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdi,rbx
       mov       rdx,r13
       mov       rsi,75C4762AAD00
       call      qword ptr [75C47607EE98]; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].CopyProjectionIndependentStateTo[[System.__Canon, System.Private.CoreLib]](NextORM.Core.EntityBuilder`1<System.__Canon>)
       mov       rsi,[rbx+18]
       lea       rdi,[r13+18]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rsi,[rbx+20]
       lea       rdi,[r13+20]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rsi,[rbx+88]
       lea       rdi,[r13+88]
       call      CORINFO_HELP_ASSIGN_REF
       cmp       qword ptr [rbx+68],0
       jne       near ptr M10_L13
       xor       esi,esi
M10_L00:
       lea       rdi,[r13+68]
       call      CORINFO_HELP_ASSIGN_REF
       cmp       qword ptr [rbx+70],0
       jne       near ptr M10_L14
       xor       esi,esi
M10_L01:
       lea       rdi,[r13+70]
       call      CORINFO_HELP_ASSIGN_REF
       mov       edi,[rbx+138]
       mov       [r13+138],edi
       mov       rsi,[rbx+78]
       lea       rdi,[r13+78]
       call      CORINFO_HELP_ASSIGN_REF
       movzx     esi,byte ptr [rbx+144]
       mov       [r13+144],sil
       cmp       qword ptr [rbx+98],0
       jne       near ptr M10_L10
       xor       esi,esi
M10_L02:
       lea       rdi,[r13+98]
       call      CORINFO_HELP_ASSIGN_REF
       cmp       qword ptr [rbx+0A0],0
       jne       near ptr M10_L11
       xor       esi,esi
M10_L03:
       lea       rdi,[r13+0A0]
       call      CORINFO_HELP_ASSIGN_REF
       cmp       qword ptr [rbx+0A8],0
       jne       near ptr M10_L15
       xor       esi,esi
M10_L04:
       lea       rdi,[r13+0A8]
       call      CORINFO_HELP_ASSIGN_REF
       movzx     edi,byte ptr [rbx+145]
       mov       [r13+145],dil
M10_L05:
       mov       rdi,[r14+30]
       mov       rdi,[rdi]
       mov       rdi,[rdi+18]
       test      rdi,rdi
       je        near ptr M10_L12
M10_L06:
       mov       r12,r13
       test      r12,r12
       je        short M10_L07
       cmp       [r12],rdi
       je        short M10_L07
       mov       rsi,r13
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       r12,rax
M10_L07:
       cmp       qword ptr [rbx+18],0
       jne       near ptr M10_L17
M10_L08:
       lea       rdi,[r12+18]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
M10_L09:
       mov       rax,r12
       add       rsp,18
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M10_L10:
       mov       rsi,[rbx+98]
       mov       rdi,75C476FF5200
       call      qword ptr [75C47607C8A0]; System.Linq.Enumerable.ToList[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
       mov       rsi,rax
       jmp       near ptr M10_L02
M10_L11:
       mov       rsi,[rbx+0A0]
       mov       rdi,75C476FF5288
       call      qword ptr [75C47607C8A0]; System.Linq.Enumerable.ToList[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
       mov       rsi,rax
       jmp       near ptr M10_L03
M10_L12:
       mov       rdi,r14
       mov       rsi,75C47627FB98
       call      qword ptr [75C47504F270]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdi,rax
       jmp       near ptr M10_L06
M10_L13:
       mov       rsi,[rbx+68]
       mov       rdi,75C4762AAC78
       call      qword ptr [75C47607C8A0]; System.Linq.Enumerable.ToList[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
       mov       rsi,rax
       jmp       near ptr M10_L00
M10_L14:
       mov       rsi,[rbx+70]
       mov       rdi,75C4762AABF0
       call      qword ptr [75C47607C8A0]; System.Linq.Enumerable.ToList[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
       mov       rsi,rax
       jmp       near ptr M10_L01
M10_L15:
       mov       rsi,[rbx+0A8]
       mov       rdi,75C4762AA640
       call      qword ptr [75C47607C8A0]; System.Linq.Enumerable.ToList[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
       mov       rsi,rax
       jmp       near ptr M10_L04
M10_L16:
       mov       rdi,rbx
       mov       rax,[r14+40]
       call      qword ptr [rax+30]
       mov       r13,rax
       jmp       near ptr M10_L05
M10_L17:
       test      r15,r15
       je        near ptr M10_L08
       mov       rdi,[rbx+18]
       mov       rax,[rdi]
       mov       rax,[rax+48]
       call      qword ptr [rax+30]
       mov       r13,rax
       mov       rdi,offset MT_NextORM.Core.ReplaceParameterExpressionVisitor
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-30],rax
       mov       rdi,[r13+8]
       mov       r11,75C4740626E0
       xor       esi,esi
       call      qword ptr [r11]
       mov       r13,[rbp-30]
       lea       rdi,[r13+8]
       mov       rsi,rax
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdi,[rbx+18]
       mov       rax,[rdi+8]
       mov       [rbp-38],rax
       mov       rdi,[r15+8]
       test      rdi,rdi
       jne       short M10_L18
       mov       rax,[rbp-38]
       xor       edi,edi
       xor       esi,esi
       jmp       short M10_L19
M10_L18:
       mov       rsi,r13
       mov       rcx,[rdi]
       mov       rcx,[rcx+48]
       call      qword ptr [rcx+8]
       mov       rsi,rax
       mov       rax,[rbp-38]
M10_L19:
       mov       rdi,rax
       xor       edx,edx
       call      qword ptr [75C476F261A8]
       mov       r15,rax
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       mov       esi,1
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       r13,rax
       mov       rdi,[rbx+18]
       mov       rax,[rdi]
       mov       rax,[rax+48]
       call      qword ptr [rax+30]
       mov       rdi,[rax+8]
       mov       r11,75C4740626E8
       xor       esi,esi
       call      qword ptr [r11]
       lea       rdi,[r13+10]
       mov       rsi,rax
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdi,[r14+30]
       mov       rdi,[rdi]
       mov       rdi,[rdi+10]
       test      rdi,rdi
       je        short M10_L20
       jmp       short M10_L21
M10_L20:
       mov       rdi,r14
       mov       rsi,75C47627FAD0
       call      qword ptr [75C47504F270]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdi,rax
M10_L21:
       mov       rsi,[rdi+18]
       mov       rax,[rsi+18]
       test      rax,rax
       je        short M10_L22
       mov       rdi,rax
       jmp       short M10_L23
M10_L22:
       mov       rsi,75C476F32758
       call      qword ptr [75C47504F3A8]; System.Runtime.CompilerServices.GenericsHelpers.Method(IntPtr, IntPtr)
       mov       rdi,rax
M10_L23:
       mov       rsi,[rdi+18]
       mov       rsi,[rsi+18]
       test      rsi,rsi
       je        short M10_L24
       jmp       short M10_L25
M10_L24:
       mov       rsi,75C476F327A8
       call      qword ptr [75C47504F3A8]; System.Runtime.CompilerServices.GenericsHelpers.Method(IntPtr, IntPtr)
       mov       rsi,rax
M10_L25:
       mov       rdi,rsi
       mov       rsi,r15
       mov       r8,r13
       xor       edx,edx
       xor       ecx,ecx
       call      qword ptr [75C47607E9A0]; System.Linq.Expressions.Expression.Lambda[[System.__Canon, System.Private.CoreLib]](System.Linq.Expressions.Expression, System.String, Boolean, System.Collections.Generic.IEnumerable`1<System.Linq.Expressions.ParameterExpression>)
       lea       rdi,[r12+18]
       mov       rsi,rax
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M10_L09
; Total bytes of code 1005
```
```assembly
; System.Linq.Expressions.Expression.Validate(System.Type, Boolean)
       push      rbp
       push      r15
       push      rbx
       sub       rsp,10
       vzeroupper
       lea       rbp,[rsp+20]
       mov       rbx,rdi
       mov       r15d,esi
       test      rbx,rbx
       je        near ptr M11_L04
       mov       rdi,rbx
       mov       rsi,75C4704027C0
       mov       edx,0FFFFFFFF
       call      qword ptr [75C47607DFE0]; System.Dynamic.Utils.TypeUtils.ValidateType(System.Type, System.String, Int32)
       test      eax,eax
       je        short M11_L01
       test      r15b,r15b
       je        short M11_L02
M11_L00:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+58]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M11_L08
M11_L01:
       mov       rdi,75C470401840
       cmp       rbx,rdi
       je        near ptr M11_L09
       add       rsp,10
       pop       rbx
       pop       r15
       pop       rbp
       ret
M11_L02:
       mov       rdi,offset MT_System.RuntimeType
       cmp       [rbx],rdi
       jne       short M11_L06
       mov       [rbp-18],rbx
       mov       rdi,[rbx+18]
       mov       rax,75C4F3474CD0
       call      rax
       movzx     ebx,al
       mov       r15,[rbp-18]
       cmp       dword ptr [75C4F36CEC80],0
       jne       short M11_L05
M11_L03:
       cmp       ebx,10
       mov       rbx,r15
       jne       short M11_L00
       jmp       short M11_L07
M11_L04:
       mov       edi,2ECB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F258]
       int       3
M11_L05:
       call      CORINFO_HELP_POLL_GC
       jmp       short M11_L03
M11_L06:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+58]
       call      qword ptr [rax+18]
       test      eax,eax
       je        near ptr M11_L00
M11_L07:
       mov       edi,2ECB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F780]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M11_L08:
       mov       edi,2ECB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F798]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M11_L09:
       mov       edi,2ECB
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476F246C0]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 350
```
```assembly
; System.RuntimeType.GetCorElementType()
       push      rbx
       sub       rsp,10
       vzeroupper
       mov       rbx,rdi
       mov       [rsp+8],rbx
       mov       rdi,[rbx+18]
       mov       rax,75C4F3474CD0
       call      rax
       movzx     ebx,al
       cmp       dword ptr [75C4F36CEC80],0
       jne       short M12_L01
M12_L00:
       mov       eax,ebx
       add       rsp,10
       pop       rbx
       ret
M12_L01:
       call      CORINFO_HELP_POLL_GC
       jmp       short M12_L00
; Total bytes of code 59
```
```assembly
; System.Linq.Expressions.ParameterExpression.Make(System.Type, System.String, Boolean)
       push      rbp
       push      r15
       push      r14
       push      rbx
       push      rax
       lea       rbp,[rsp+20]
       mov       rbx,rdi
       mov       r15,rsi
M13_L00:
       test      dl,dl
       jne       near ptr M13_L07
       mov       rdi,offset MT_System.RuntimeType
       cmp       [rbx],rdi
       jne       near ptr M13_L09
       mov       rdi,[rbx+18]
       test      dil,2
       jne       near ptr M13_L08
       mov       rdi,[rdi+10]
       mov       rsi,offset MT_System.Enum
       cmp       rdi,rsi
       sete      al
       movzx     eax,al
M13_L01:
       test      eax,eax
       jne       near ptr M13_L04
       mov       rdi,offset MT_System.RuntimeType
       cmp       [rbx],rdi
       jne       near ptr M13_L30
       mov       rdi,[rbx+10]
       test      rdi,rdi
       je        near ptr M13_L06
       mov       rax,[rdi]
       test      rax,rax
       je        near ptr M13_L06
M13_L02:
       mov       r14d,[rax+98]
       test      r14d,r14d
       je        near ptr M13_L10
M13_L03:
       dec       r14d
       jne       near ptr M13_L31
       mov       rdi,75C470401D88
       cmp       rbx,rdi
       je        near ptr M13_L32
       mov       rdi,75C470406468
       cmp       rbx,rdi
       je        near ptr M13_L33
       mov       rdi,75C470406490
       cmp       rbx,rdi
       je        near ptr M13_L34
M13_L04:
       mov       rdi,offset MT_System.Linq.Expressions.TypedParameterExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       lea       rdi,[r14+10]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
M13_L05:
       mov       rax,r14
       add       rsp,8
       pop       rbx
       pop       r14
       pop       r15
       pop       rbp
       ret
M13_L06:
       mov       rdi,rbx
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
       jmp       near ptr M13_L02
M13_L07:
       mov       rdi,offset MT_System.Linq.Expressions.ByRefParameterExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       lea       rdi,[r14+10]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       jmp       short M13_L05
M13_L08:
       mov       rdi,rbx
       mov       rsi,75C470409F00
       call      qword ptr [75C47405A510]; Precode of System.RuntimeType.IsSubclassOf(System.Type)
       jmp       near ptr M13_L01
M13_L09:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+70]
       call      qword ptr [rax+30]
       jmp       near ptr M13_L01
M13_L10:
       mov       r14,rbx
       mov       rdi,r14
       call      qword ptr [75C47607E220]; System.RuntimeType.get_IsActualEnum()
       test      eax,eax
       je        short M13_L11
       mov       rdi,rbx
       call      qword ptr [75C47405A490]
       mov       r14,rax
       test      r14,r14
       je        short M13_L11
       mov       rdi,offset MT_System.RuntimeType
       cmp       [r14],rdi
       je        short M13_L11
       mov       rsi,rax
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       int       3
M13_L11:
       mov       rdi,75C470406288
       cmp       r14,rdi
       jne       short M13_L12
       mov       r14d,5
       jmp       near ptr M13_L27
M13_L12:
       mov       rdi,75C4704062B0
       cmp       r14,rdi
       jne       short M13_L13
       mov       r14d,6
       jmp       near ptr M13_L27
M13_L13:
       mov       rdi,75C4704062D8
       cmp       r14,rdi
       jne       short M13_L14
       mov       r14d,7
       jmp       near ptr M13_L27
M13_L14:
       mov       rdi,75C470406300
       cmp       r14,rdi
       jne       short M13_L15
       mov       r14d,8
       jmp       near ptr M13_L27
M13_L15:
       mov       rdi,75C4704021B0
       cmp       r14,rdi
       jne       short M13_L16
       mov       r14d,9
       jmp       near ptr M13_L27
M13_L16:
       mov       rdi,75C470406328
       cmp       r14,rdi
       jne       short M13_L17
       mov       r14d,0A
       jmp       near ptr M13_L27
M13_L17:
       mov       rdi,75C470402240
       cmp       r14,rdi
       jne       short M13_L18
       mov       r14d,0B
       jmp       near ptr M13_L27
M13_L18:
       mov       rdi,75C470406350
       cmp       r14,rdi
       jne       short M13_L19
       mov       r14d,0C
       jmp       near ptr M13_L27
M13_L19:
       mov       rdi,75C470406378
       cmp       r14,rdi
       jne       short M13_L20
       mov       r14d,3
       jmp       near ptr M13_L27
M13_L20:
       mov       rdi,75C4704063A0
       cmp       r14,rdi
       jne       short M13_L21
       mov       r14d,4
       jmp       near ptr M13_L27
M13_L21:
       mov       rdi,75C4704063C8
       cmp       r14,rdi
       jne       short M13_L22
       mov       r14d,0D
       jmp       short M13_L27
M13_L22:
       mov       rdi,75C4704063F0
       cmp       r14,rdi
       jne       short M13_L23
       mov       r14d,0E
       jmp       short M13_L27
M13_L23:
       mov       rdi,75C470402ED0
       cmp       r14,rdi
       jne       short M13_L24
       mov       r14d,0F
       jmp       short M13_L27
M13_L24:
       mov       rdi,75C470406418
       cmp       r14,rdi
       jne       short M13_L25
       mov       r14d,10
       jmp       short M13_L27
M13_L25:
       mov       rdi,75C470400020
       cmp       r14,rdi
       jne       short M13_L26
       mov       r14d,12
       jmp       short M13_L27
M13_L26:
       mov       rdi,75C470406440
       mov       eax,1
       mov       ecx,2
       cmp       r14,rdi
       cmove     eax,ecx
       mov       r14d,eax
M13_L27:
       mov       rdi,[rbx+10]
       test      rdi,rdi
       je        short M13_L28
       mov       rax,[rdi]
       test      rax,rax
       je        short M13_L28
       jmp       short M13_L29
M13_L28:
       mov       rdi,rbx
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
M13_L29:
       mov       [rax+98],r14d
       jmp       near ptr M13_L03
M13_L30:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+98]
       call      qword ptr [rax+10]
       mov       r14d,eax
       jmp       near ptr M13_L03
M13_L31:
       cmp       r14d,11
       ja        near ptr M13_L04
       mov       edi,r14d
       lea       rax,[75C476F78CE0]
       mov       eax,[rax+rdi*4]
       lea       rcx,[M13_L00]
       add       rax,rcx
       jmp       rax
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Boolean>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Byte>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Char>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.DateTime>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Decimal>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Double>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Int16>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Int32>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Int64>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
M13_L32:
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
M13_L33:
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Exception>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
M13_L34:
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Object[]>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.SByte>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.Single>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.UInt16>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.UInt32>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
       mov       rdi,offset MT_System.Linq.Expressions.PrimitiveParameterExpression<System.UInt64>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       lea       rdi,[r14+8]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M13_L05
; Total bytes of code 1562
```
```assembly
; System.RuntimeMethodInfoStub.FromPtr(IntPtr)
       push      rbp
       push      r15
       push      rbx
       lea       rbp,[rsp+10]
       mov       rbx,rdi
       test      rbx,rbx
       je        short M14_L00
       mov       rdi,offset MT_System.RuntimeMethodInfoStub
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,rbx
       call      000075C4F3501E00
       lea       rdi,[r15+8]
       mov       rsi,rax
       call      CORINFO_HELP_ASSIGN_REF
       mov       [r15+50],rbx
       mov       rax,r15
       pop       rbx
       pop       r15
       pop       rbp
       ret
M14_L00:
       mov       rdi,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476E8F360]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C476077558]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 112
```
```assembly
; System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      rbx
       lea       rbp,[rsp+20]
       mov       rbx,rdi
       test      rbx,rbx
       je        near ptr M15_L07
       mov       rdi,offset MT_System.RuntimeMethodInfoStub
       cmp       [rbx],rdi
       jne       short M15_L02
       mov       rsi,[rbx+50]
M15_L00:
       xor       edi,edi
       call      qword ptr [75C475D05E18]; System.RuntimeType.GetMethodBase(System.RuntimeType, System.RuntimeMethodHandleInternal)
       mov       r15,rax
       test      r15,r15
       je        near ptr M15_L08
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [r15],rdi
       jne       near ptr M15_L10
       mov       rdi,[r15+8]
       cmp       byte ptr [rdi+9C],0
       jne       near ptr M15_L09
       mov       rbx,[r15+38]
M15_L01:
       test      rbx,rbx
       je        short M15_L06
       mov       rdi,offset MT_System.RuntimeType
       cmp       [rbx],rdi
       jne       near ptr M15_L12
       mov       rdi,[rbx+18]
       test      dil,2
       jne       near ptr M15_L11
       test      dword ptr [rdi],80000000
       jne       short M15_L03
       test      byte ptr [rdi],30
       setne     al
       movzx     eax,al
       jmp       short M15_L04
M15_L02:
       mov       rdi,rbx
       mov       r11,75C474062238
       call      qword ptr [r11]
       mov       rsi,rax
       jmp       near ptr M15_L00
M15_L03:
       xor       eax,eax
M15_L04:
       movzx     r14d,al
M15_L05:
       test      r14d,r14d
       jne       short M15_L13
M15_L06:
       mov       rax,r15
       pop       rbx
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M15_L07:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [75C476F24768]
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C4760769D0]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M15_L08:
       xor       ebx,ebx
       jmp       near ptr M15_L01
M15_L09:
       xor       ebx,ebx
       jmp       near ptr M15_L01
M15_L10:
       mov       rdi,r15
       mov       rax,[r15]
       mov       rax,[rax+40]
       call      qword ptr [rax+38]
       mov       rbx,rax
       jmp       near ptr M15_L01
M15_L11:
       xor       eax,eax
       jmp       short M15_L04
M15_L12:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+60]
       call      qword ptr [rax+8]
       mov       r14d,eax
       jmp       short M15_L05
M15_L13:
       call      qword ptr [75C476F24780]
       mov       r14,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+68]
       call      qword ptr [rax+18]
       mov       rdx,rax
       mov       rsi,r15
       mov       rdi,r14
       call      qword ptr [75C476F24798]
       mov       rsi,rax
       mov       rdi,r13
       call      qword ptr [75C4760769D0]
       mov       rdi,r13
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 386
```
```assembly
; System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.MethodInfo)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,58
       lea       rbp,[rsp+80]
       mov       r15,rdi
       mov       rbx,rsi
       test      rbx,rbx
       je        near ptr M16_L16
       mov       r14,[rbx]
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       r14,rdi
       jne       near ptr M16_L17
       mov       rdi,rbx
       call      qword ptr [75C4754A6028]; System.Reflection.RuntimeMethodInfo.get_ContainsGenericParameters()
M16_L00:
       test      eax,eax
       jne       near ptr M16_L18
       mov       rdi,rbx
       mov       r13,[r14+40]
       call      qword ptr [r13+38]
       mov       r13,rax
       test      r13,r13
       je        near ptr M16_L25
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       r14,rdi
       jne       near ptr M16_L21
       mov       r12d,[rbx+5C]
M16_L01:
       mov       esi,8
       mov       edi,4
       test      r12b,10
       cmove     esi,edi
       or        esi,30
       mov       rdi,r13
       mov       rax,[r13]
       mov       rax,[rax+90]
       call      qword ptr [rax+38]
       mov       r13,rax
       xor       r12d,r12d
       jmp       near ptr M16_L06
M16_L02:
       mov       rdi,[rax+18]
       test      dil,2
       jne       near ptr M16_L22
       mov       edi,[rdi]
       and       edi,0F0000
       cmp       edi,0C0000
       sete      dil
       movzx     edi,dil
M16_L03:
       test      edi,edi
       jne       near ptr M16_L07
M16_L04:
       mov       rdi,r14
       mov       rax,[r14]
       mov       [rbp-48],rax
       mov       rcx,[rax+50]
       call      qword ptr [rcx+30]
       test      eax,eax
       je        short M16_L05
       mov       rdi,r14
       mov       esi,1
       mov       rax,[rbp-48]
       mov       rax,[rax+58]
       call      qword ptr [rax+18]
       mov       [rbp-68],rax
       mov       rdi,rbx
       mov       rsi,rax
       mov       rcx,[rbx]
       mov       rcx,[rcx+40]
       mov       [rbp-30],rcx
       call      qword ptr [rcx+10]
       test      eax,eax
       jne       near ptr M16_L09
       mov       rdi,rbx
       mov       rax,[rbp-30]
       call      qword ptr [rax+38]
       mov       [rbp-70],rax
       mov       rcx,[rax]
       mov       [rbp-40],rcx
       mov       rdi,offset MT_System.RuntimeType
       cmp       rcx,rdi
       je        near ptr M16_L11
       mov       rdi,rax
       mov       rdx,[rcx+70]
       call      qword ptr [rdx+18]
       test      al,20
       jne       near ptr M16_L13
M16_L05:
       inc       r12d
M16_L06:
       mov       edi,[r13+8]
       cmp       edi,r12d
       jle       near ptr M16_L25
       mov       edi,r12d
       mov       r14,[r13+rdi*8+10]
       mov       rdi,r14
       mov       rax,[r14]
       mov       [rbp-48],rax
       mov       rcx,[rax+50]
       call      qword ptr [rcx+28]
       test      eax,eax
       je        near ptr M16_L04
       mov       rdi,r14
       mov       esi,1
       mov       rax,[rbp-48]
       mov       rax,[rax+58]
       call      qword ptr [rax+8]
       mov       [rbp-50],rax
       mov       rdi,rbx
       mov       rsi,rax
       mov       rcx,[rbx]
       mov       rcx,[rcx+40]
       mov       [rbp-30],rcx
       call      qword ptr [rcx+10]
       test      eax,eax
       jne       near ptr M16_L09
       mov       rdi,rbx
       mov       rax,[rbp-30]
       call      qword ptr [rax+38]
       mov       [rbp-58],rax
       mov       rcx,[rax]
       mov       [rbp-38],rcx
       mov       rdi,offset MT_System.RuntimeType
       cmp       rcx,rdi
       je        near ptr M16_L02
       mov       rdi,rax
       mov       rdx,[rcx+70]
       call      qword ptr [rdx+18]
       test      al,20
       je        near ptr M16_L04
M16_L07:
       mov       rdi,rbx
       mov       rdx,[rbp-30]
       call      qword ptr [rdx+30]
       mov       [rbp-60],rax
       mov       rdi,[rbp-50]
       mov       rcx,[rdi]
       mov       rcx,[rcx+40]
       call      qword ptr [rcx+30]
       mov       rsi,rax
       mov       rdx,[rbp-60]
       cmp       rdx,rsi
       jne       near ptr M16_L10
M16_L08:
       mov       rdi,rbx
       mov       rax,[rbp-30]
       call      qword ptr [rax+30]
       test      rax,rax
       je        near ptr M16_L24
       xor       edi,edi
       mov       [rsp],rdi
       mov       rdi,[rbp-58]
       mov       rsi,rax
       mov       edx,1C
       xor       ecx,ecx
       mov       r8d,3
       xor       r9d,r9d
       mov       rax,[rbp-38]
       mov       rax,[rax+88]
       call      qword ptr [rax+38]
       mov       rsi,[rbp-50]
       cmp       rax,rsi
       je        short M16_L09
       test      rax,rax
       je        near ptr M16_L04
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       test      eax,eax
       je        near ptr M16_L04
M16_L09:
       mov       rdi,r15
       mov       rsi,r14
       add       rsp,58
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       jmp       qword ptr [75C47607E298]; System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.PropertyInfo)
M16_L10:
       test      rdx,rdx
       je        near ptr M16_L04
       test      rsi,rsi
       je        near ptr M16_L04
       mov       edi,[rdx+8]
       cmp       edi,[rsi+8]
       jne       near ptr M16_L04
       lea       rdi,[rdx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       add       rsi,0C
       call      qword ptr [75C475047A38]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       test      eax,eax
       je        near ptr M16_L04
       jmp       near ptr M16_L08
M16_L11:
       mov       rdi,[rax+18]
       test      dil,2
       jne       near ptr M16_L23
       mov       edi,[rdi]
       and       edi,0F0000
       cmp       edi,0C0000
       sete      dil
       movzx     edi,dil
M16_L12:
       test      edi,edi
       je        near ptr M16_L05
M16_L13:
       mov       rdi,rbx
       mov       rdx,[rbp-30]
       call      qword ptr [rdx+30]
       mov       [rbp-78],rax
       mov       rdi,[rbp-68]
       mov       rax,[rdi]
       mov       rax,[rax+40]
       call      qword ptr [rax+30]
       mov       rsi,rax
       mov       rdi,[rbp-78]
       cmp       rdi,rsi
       jne       short M16_L15
M16_L14:
       mov       rdi,rbx
       mov       rax,[rbp-30]
       call      qword ptr [rax+30]
       test      rax,rax
       je        near ptr M16_L24
       xor       edi,edi
       mov       [rsp],rdi
       mov       rdi,[rbp-70]
       mov       rsi,rax
       mov       edx,1C
       xor       ecx,ecx
       mov       r8d,3
       xor       r9d,r9d
       mov       rax,[rbp-40]
       mov       rax,[rax+88]
       call      qword ptr [rax+38]
       mov       rsi,[rbp-68]
       cmp       rax,rsi
       je        near ptr M16_L09
       test      rax,rax
       je        near ptr M16_L05
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       test      eax,eax
       jne       near ptr M16_L09
       jmp       near ptr M16_L05
M16_L15:
       test      rdi,rdi
       je        near ptr M16_L05
       test      rsi,rsi
       je        near ptr M16_L05
       mov       edx,[rdi+8]
       cmp       edx,[rsi+8]
       jne       near ptr M16_L05
       add       rdi,0C
       add       edx,edx
       add       rsi,0C
       call      qword ptr [75C475047A38]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       test      eax,eax
       je        near ptr M16_L05
       jmp       near ptr M16_L14
M16_L16:
       mov       edi,3167
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F258]
       int       3
M16_L17:
       mov       rdi,rbx
       mov       rax,[r14+58]
       call      qword ptr [rax+28]
       jmp       near ptr M16_L00
M16_L18:
       mov       rdi,rbx
       mov       rax,[r14+58]
       call      qword ptr [rax+18]
       test      eax,eax
       jne       short M16_L19
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,75C4704064F0
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,rbx
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,r15
       mov       rdx,75C4704064B8
       call      qword ptr [75C4764A5CB0]
       jmp       short M16_L20
M16_L19:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rdi,75C470406548
       call      qword ptr [75C476E8F750]
       mov       rdi,rax
       mov       rsi,rbx
       call      qword ptr [75C476E8F768]
       mov       rsi,rax
       mov       rdi,r15
       mov       rdx,75C4704064B8
       call      qword ptr [75C4764A5CB0]
M16_L20:
       mov       rdi,r15
       call      CORINFO_HELP_THROW
       int       3
M16_L21:
       mov       rdi,rbx
       mov       rax,[r14+50]
       call      qword ptr [rax+20]
       mov       r12d,eax
       jmp       near ptr M16_L01
M16_L22:
       xor       edi,edi
       jmp       near ptr M16_L03
M16_L23:
       xor       edi,edi
       jmp       near ptr M16_L12
M16_L24:
       mov       edi,5AD
       mov       rsi,75C474054000
       call      qword ptr [75C47504EF88]
       mov       rdi,rax
       call      qword ptr [75C476E8F258]
       int       3
M16_L25:
       mov       rdi,rbx
       mov       rcx,[rbx]
       mov       r14,rcx
       mov       rax,[r14+40]
       mov       r13,rax
       call      qword ptr [r13+38]
       mov       r14,rax
       mov       rdi,rbx
       call      qword ptr [r13+30]
       mov       rbx,rax
       mov       edi,3167
       mov       rsi,75C4754D5648
       call      qword ptr [75C47504EF88]
       mov       rdx,rax
       mov       rdi,r14
       mov       rsi,rbx
       mov       ecx,0FFFFFFFF
       call      qword ptr [75C476F248B8]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 1341
```
```assembly
; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].Select[[System.Int32, System.Private.CoreLib]](System.Linq.Expressions.Expression`1<System.Func`2<System.__Canon,Int32>>)
       push      rbp
       push      r15
       push      r14
       push      rbx
       push      rax
       lea       rbp,[rsp+20]
       mov       [rbp-20],rsi
       mov       rbx,rdi
       mov       r15,rdx
       mov       rdi,[rbx+0A0]
       test      rdi,rdi
       jne       short M17_L04
M17_L00:
       mov       rdi,[rbx+98]
       test      rdi,rdi
       jne       near ptr M17_L05
M17_L01:
       cmp       byte ptr [rbx+145],0
       jne       near ptr M17_L06
       mov       rdi,[rsi+18]
       mov       rax,[rdi+10]
       test      rax,rax
       je        short M17_L03
       mov       rsi,rax
M17_L02:
       mov       rdi,rbx
       mov       rdx,r15
       add       rsp,8
       pop       rbx
       pop       r14
       pop       r15
       pop       rbp
       jmp       qword ptr [75C47607F8B8]; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].SelectCore[[System.Int32, System.Private.CoreLib]](System.Linq.Expressions.Expression`1<System.Func`2<System.__Canon,Int32>>)
M17_L03:
       mov       rdi,rsi
       mov       rsi,75C4762B2FA8
       call      qword ptr [75C47504F3A8]; System.Runtime.CompilerServices.GenericsHelpers.Method(IntPtr, IntPtr)
       mov       rsi,rax
       jmp       short M17_L02
M17_L04:
       cmp       dword ptr [rdi+10],0
       jle       short M17_L00
       mov       rdi,offset MT_System.NotSupportedException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       edi,3C76
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       r15,rax
       mov       edi,2842
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       r14,rax
       mov       edi,3CBA
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       rdx,rax
       mov       rdi,r15
       mov       rsi,r14
       call      qword ptr [75C47543F708]; System.String.Concat(System.String, System.String, System.String)
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C47504EFB8]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M17_L05:
       cmp       dword ptr [rdi+10],0
       jle       near ptr M17_L01
       mov       rdi,offset MT_System.NotSupportedException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       edi,3D65
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       r15,rax
       mov       edi,2842
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       r14,rax
       mov       edi,3DA9
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       rdx,rax
       mov       rdi,r15
       mov       rsi,r14
       call      qword ptr [75C47543F708]; System.String.Concat(System.String, System.String, System.String)
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C47504EFB8]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
M17_L06:
       mov       rdi,offset MT_System.NotSupportedException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       edi,3E94
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       r15,rax
       mov       edi,2842
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       r14,rax
       mov       edi,3EE2
       mov       rsi,75C4754D4150
       call      qword ptr [75C47504EF88]
       mov       rdx,rax
       mov       rdi,r15
       mov       rsi,r14
       call      qword ptr [75C47543F708]; System.String.Concat(System.String, System.String, System.String)
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [75C47504EFB8]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 510
```
```assembly
; System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointer(System.Object, IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       lea       rbp,[rsp+28]
       mov       rax,[rdi]
       mov       ecx,esi
       rol       ecx,5
       add       ecx,eax
       mov       r8d,edx
       ror       r8d,5
       add       ecx,r8d
       mov       r8,75BE850003A0
       mov       r8,[r8]
       mov       r8,[r8+8]
       movsxd    r9,ecx
       mov       r10,9E3779B97F4A7C15
       imul      r9,r10
       movzx     r10d,byte ptr [r8+10]
       shrx      r9,r9,r10
       xor       r10d,r10d
       jmp       short M18_L01
M18_L00:
       test      ebx,ebx
       je        short M18_L02
       inc       r10d
       add       r9d,r10d
       mov       r11d,[r8+8]
       add       r11d,0FFFFFFFE
       and       r9d,r11d
       cmp       r10d,8
       jge       short M18_L02
M18_L01:
       lea       r11d,[r9+1]
       movsxd    r11,r11d
       imul      r11,30
       lea       r11,[r8+r11+10]
       mov       ebx,[r11]
       mov       r15d,[r11+8]
       mov       r14,[r11+10]
       mov       r13,[r11+18]
       mov       r12,[r11+20]
       cmp       ecx,r15d
       jne       short M18_L00
       mov       r15,rax
       sub       r15,r14
       mov       r14,rsi
       sub       r14,r13
       or        r15,r14
       mov       r14,rdx
       sub       r14,r12
       or        r15,r14
       jne       short M18_L00
       mov       rax,[r11+28]
       and       ebx,0FFFFFFFE
       cmp       ebx,[r11]
       jne       short M18_L02
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M18_L02:
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       jmp       qword ptr [75C47543FE88]; System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointerSlow(System.Object, IntPtr, IntPtr)
; Total bytes of code 216
```
```assembly
; System.RuntimeType.GetMethodBase(System.RuntimeType, System.RuntimeMethodHandleInternal)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,58
       lea       rbp,[rsp+80]
       xor       eax,eax
       mov       [rbp-38],rax
       mov       rbx,rdi
       mov       r15,rsi
       mov       rdi,r15
       call      000075C4F3500730
       test      eax,eax
       jne       near ptr M19_L13
       mov       rdi,r15
       call      000075C4F34FEEB0
       mov       rdi,[rax+20]
       add       rdi,10
       mov       r14,[rdi]
       test      r14,r14
       je        near ptr M19_L15
M19_L00:
       mov       r13,r14
       xor       r12d,r12d
       test      rbx,rbx
       cmove     rbx,r13
       cmp       rbx,r13
       jne       near ptr M19_L16
M19_L01:
       test      r12,r12
       jne       near ptr M19_L09
       mov       rdi,r15
       mov       rsi,r13
       call      000075C4F3500FC0
       mov       r14,rax
       test      r14,r14
       je        near ptr M19_L09
M19_L02:
       mov       rdi,r14
       call      000075C4F3501D90
       test      eax,eax
       jne       near ptr M19_L11
       mov       rdi,r14
       call      000075C4F35006C0
       test      eax,eax
       jne       near ptr M19_L28
M19_L03:
       mov       rdi,[rbx+10]
       test      rdi,rdi
       je        near ptr M19_L10
       mov       r15,[rdi]
       test      r15,r15
       je        short M19_L10
M19_L04:
       cmp       [r15],r15b
       lea       rbx,[r15+40]
       cmp       qword ptr [rbx],0
       je        near ptr M19_L31
M19_L05:
       mov       rbx,[r15+40]
       mov       rdi,[rbx+8]
       test      rdi,rdi
       je        near ptr M19_L33
       xor       eax,eax
       cmp       dword ptr [rdi+8],0
       jle       near ptr M19_L33
M19_L06:
       mov       rcx,[rdi+rax*8+10]
       test      rcx,rcx
       je        near ptr M19_L33
       mov       rdx,[rcx+50]
       cmp       rdx,r14
       jne       near ptr M19_L32
       mov       rax,rcx
M19_L07:
       xor       edi,edi
       mov       [rbp-38],rdi
M19_L08:
       add       rsp,58
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M19_L09:
       mov       rdi,r15
       mov       rsi,r13
       mov       rdx,r12
       call      qword ptr [75C476075530]; System.RuntimeMethodHandle.<GetStubIfNeeded>g__GetStubIfNeededWorker|47_0(System.RuntimeMethodHandleInternal, System.RuntimeType, System.RuntimeType[])
       mov       r14,rax
       jmp       near ptr M19_L02
M19_L10:
       mov       rdi,rbx
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
       mov       r15,rax
       jmp       near ptr M19_L04
M19_L11:
       mov       rdi,[rbx+10]
       test      rdi,rdi
       je        near ptr M19_L27
       mov       r15,[rdi]
       test      r15,r15
       je        near ptr M19_L27
M19_L12:
       cmp       [r15],r15b
       lea       rdx,[r15+48]
       mov       rdi,r15
       mov       rsi,75C475EB1578
       call      qword ptr [75C47543C8A0]; System.RuntimeType+RuntimeTypeCache.GetMemberCache[[System.__Canon, System.Private.CoreLib]](MemberInfoCache`1<System.__Canon> ByRef)
       mov       rdi,[r15+48]
       mov       rsi,r13
       mov       rdx,r14
       mov       ecx,1
       cmp       [rdi],edi
       call      qword ptr [75C475D05E90]; System.RuntimeType+RuntimeTypeCache+MemberInfoCache`1[[System.__Canon, System.Private.CoreLib]].AddMethod(System.RuntimeType, System.RuntimeMethodHandleInternal, CacheType)
       jmp       near ptr M19_L08
M19_L13:
       mov       rdi,r15
       call      000075C4F3500750
       test      rax,rax
       je        short M19_L14
       mov       rdi,rax
       mov       rax,[rax]
       mov       rax,[rax+48]
       call      qword ptr [rax+20]
       nop
       add       rsp,58
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M19_L14:
       xor       eax,eax
       add       rsp,58
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M19_L15:
       mov       rdi,rax
       call      qword ptr [75C475045C68]; System.RuntimeTypeHandle.GetRuntimeTypeFromHandleSlow(IntPtr)
       mov       r14,rax
       jmp       near ptr M19_L00
M19_L16:
       mov       rdi,rbx
       mov       rsi,r13
       cmp       [rdi],edi
       call      qword ptr [75C47405A510]; Precode of System.RuntimeType.IsSubclassOf(System.Type)
       test      eax,eax
       jne       near ptr M19_L01
       mov       rdi,rbx
       call      qword ptr [75C476F24810]
       test      eax,eax
       je        near ptr M19_L20
       mov       rdi,r15
       call      qword ptr [75C476F24828]
       mov       rsi,rax
       mov       rdi,rbx
       mov       edx,9
       mov       ecx,34
       call      qword ptr [75C47405A3C8]
       mov       rsi,rax
       mov       rdi,offset MT_System.Reflection.MethodBase[]
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfAny(Void*, System.Object)
       mov       r14,rax
       xor       eax,eax
       mov       [rbp-2C],eax
       xor       ecx,ecx
       jmp       short M19_L19
M19_L17:
       mov       [rbp-48],rcx
       mov       rsi,[r14+rcx*8+10]
       mov       rdi,offset MT_System.IRuntimeMethodInfo
       call      qword ptr [75C47543EB50]; System.Runtime.CompilerServices.CastHelpers.ChkCastInterface(Void*, System.Object)
       mov       rdi,rax
       mov       r11,75C474062240
       call      qword ptr [r11]
       cmp       rax,r15
       jne       short M19_L18
       mov       edi,1
       mov       [rbp-2C],edi
M19_L18:
       mov       rcx,[rbp-48]
       inc       ecx
M19_L19:
       cmp       [r14+8],ecx
       jg        short M19_L17
       cmp       dword ptr [rbp-2C],0
       jne       near ptr M19_L01
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       call      qword ptr [75C476F24840]
       mov       rdi,rax
       mov       rsi,rbx
       mov       rdx,r13
       call      qword ptr [75C476F24798]
       mov       rsi,rax
       mov       rdi,r15
       call      qword ptr [75C4760769D0]
       mov       rdi,r15
       call      CORINFO_HELP_THROW
       int       3
M19_L20:
       mov       rdi,r13
       cmp       [rdi],edi
       call      qword ptr [75C47405A280]; System.RuntimeType.get_IsGenericType()
       test      eax,eax
       je        near ptr M19_L26
       mov       rdi,r13
       call      qword ptr [75C47405A2D0]; Precode of System.RuntimeType.GetGenericTypeDefinition()
       mov       rsi,rax
       mov       rdi,offset MT_System.RuntimeType
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       r14,rax
       mov       rax,rbx
       test      rax,rax
       je        short M19_L23
M19_L21:
       mov       [rbp-50],rax
       mov       rcx,rax
       mov       [rbp-58],rcx
       mov       rdi,rcx
       call      qword ptr [75C47405A280]; System.RuntimeType.get_IsGenericType()
       test      eax,eax
       je        short M19_L22
       mov       rdi,[rbp-58]
       call      qword ptr [75C47405A288]; Precode of System.RuntimeType.get_IsGenericTypeDefinition()
       test      eax,eax
       jne       short M19_L22
       mov       rdi,[rbp-50]
       call      qword ptr [75C47405A2D0]; Precode of System.RuntimeType.GetGenericTypeDefinition()
       mov       rsi,rax
       mov       rdi,offset MT_System.RuntimeType
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       rdi,rax
       mov       [rbp-58],rdi
M19_L22:
       mov       rdi,[rbp-58]
       cmp       rdi,r14
       je        short M19_L24
       mov       rdi,[rbp-50]
       call      qword ptr [75C475D04E10]; System.RuntimeType.GetBaseType()
       mov       rdi,rax
       test      rdi,rdi
       mov       rax,rdi
       jne       short M19_L21
M19_L23:
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r12,rax
       call      qword ptr [75C476F24840]
       mov       rdi,rax
       mov       rsi,rbx
       mov       rdx,r13
       call      qword ptr [75C476F24798]
       mov       rsi,rax
       mov       rdi,r12
       call      qword ptr [75C4760769D0]
       mov       rdi,r12
       call      CORINFO_HELP_THROW
       int       3
M19_L24:
       mov       rdi,[rbp-50]
       mov       r13,rdi
       mov       rdi,r15
       call      000075C4F35006F0
       test      eax,eax
       jne       short M19_L25
       mov       rdi,r15
       call      qword ptr [75C476F24858]
       mov       r12,rax
M19_L25:
       mov       rdi,r15
       mov       rsi,r13
       call      000075C4F35013C0
       mov       r15,rax
       jmp       near ptr M19_L01
M19_L26:
       mov       rdi,r13
       mov       rsi,rbx
       call      qword ptr [75C47405A568]; Precode of System.RuntimeType.IsAssignableFrom(System.Reflection.TypeInfo)
       test      eax,eax
       jne       near ptr M19_L01
       call      qword ptr [75C476F24840]
       mov       r12,rax
       mov       rdi,rbx
       call      qword ptr [75C47405A180]
       mov       r15,rax
       mov       rdi,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rdi,r14
       call      qword ptr [75C47405A180]
       mov       rdx,rax
       mov       rsi,r15
       mov       rdi,r12
       call      qword ptr [75C476F24798]
       mov       rsi,rax
       mov       rdi,r13
       call      qword ptr [75C4760769D0]
       mov       rdi,r13
       call      CORINFO_HELP_THROW
       int       3
M19_L27:
       mov       rdi,rbx
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
       mov       r15,rax
       jmp       near ptr M19_L12
M19_L28:
       mov       rdi,r14
       call      000075C4F35006F0
       test      eax,eax
       jne       near ptr M19_L03
       cmp       qword ptr [rbx+10],0
       je        short M19_L29
       mov       rdi,[rbx+10]
       mov       rdi,[rdi]
       test      rdi,rdi
       je        short M19_L29
       jmp       short M19_L30
M19_L29:
       mov       rdi,rbx
       call      qword ptr [75C47543C8E8]; System.RuntimeType.InitializeCache()
       mov       rdi,rax
M19_L30:
       mov       rsi,r14
       cmp       [rdi],edi
       call      qword ptr [75C47607E418]; System.RuntimeType+RuntimeTypeCache.GetGenericMethodInfo(System.RuntimeMethodHandleInternal)
       jmp       near ptr M19_L08
M19_L31:
       mov       rdi,offset MT_System.RuntimeType+RuntimeTypeCache+MemberInfoCache<System.Reflection.RuntimeMethodInfo>
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-60],rax
       lea       rdi,[rax+10]
       mov       rsi,r15
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdi,rbx
       mov       rsi,[rbp-60]
       xor       edx,edx
       call      000075C4F3474060
       jmp       near ptr M19_L05
M19_L32:
       inc       eax
       cmp       [rdi+8],eax
       jg        near ptr M19_L06
M19_L33:
       xor       edi,edi
       mov       [rbp-38],rdi
       mov       rdi,r14
       call      000075C4F34FEE60
       mov       r15d,eax
       mov       rsi,[rbx+10]
       cmp       r13,[rsi+8]
       setne     sil
       movzx     esi,sil
       mov       edi,r15d
       and       edi,7
       cmp       edi,6
       sete      dil
       movzx     edi,dil
       test      r15b,10
       setne     dl
       movzx     edx,dl
       call      qword ptr [75C47543CF30]; System.RuntimeType.FilterPreCalculate(Boolean, Boolean, Boolean)
       mov       [rbp-3C],eax
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo[]
       mov       esi,1
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       [rbp-68],rax
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-70],rax
       xor       ecx,ecx
       mov       [rsp],rcx
       mov       rcx,[rbx+10]
       mov       rdi,rax
       mov       rsi,r14
       mov       rdx,r13
       mov       r8d,r15d
       mov       r9d,[rbp-3C]
       call      qword ptr [75C476F25E78]; System.Reflection.RuntimeMethodInfo..ctor(System.RuntimeMethodHandleInternal, System.RuntimeType, RuntimeTypeCache, System.Reflection.MethodAttributes, System.Reflection.BindingFlags, System.Object)
       mov       r13,[rbp-68]
       lea       rdi,[r13+10]
       mov       rsi,[rbp-70]
       call      CORINFO_HELP_ASSIGN_REF
       mov       [rbp-38],r13
       lea       rsi,[rbp-38]
       mov       rdi,rbx
       xor       edx,edx
       mov       ecx,3
       call      qword ptr [75C47543CFC0]; System.RuntimeType+RuntimeTypeCache+MemberInfoCache`1[[System.__Canon, System.Private.CoreLib]].Insert(System.__Canon[] ByRef, System.String, MemberListType)
       mov       rdi,[rbp-38]
       cmp       dword ptr [rdi+8],0
       jbe       short M19_L34
       mov       rdi,[rbp-38]
       mov       rax,[rdi+10]
       jmp       near ptr M19_L07
M19_L34:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1444
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       push      rbp
       mov       rbp,rsp
       test      rsi,rsi
       je        short M20_L00
       mov       rax,[rsi]
       cmp       rax,rdi
       je        short M20_L00
       mov       rax,[rax+10]
       cmp       rax,rdi
       jne       short M20_L01
M20_L00:
       mov       rax,rsi
       pop       rbp
       ret
M20_L01:
       test      rax,rax
       je        short M20_L04
       mov       rax,[rax+10]
       cmp       rax,rdi
       je        short M20_L00
       jmp       short M20_L03
M20_L02:
       mov       rax,[rax+10]
       cmp       rax,rdi
       je        short M20_L00
       jmp       short M20_L01
M20_L03:
       test      rax,rax
       je        short M20_L04
       mov       rax,[rax+10]
       cmp       rax,rdi
       je        short M20_L00
       test      rax,rax
       je        short M20_L04
       mov       rax,[rax+10]
       cmp       rax,rdi
       je        short M20_L00
       test      rax,rax
       jne       short M20_L02
M20_L04:
       pop       rbp
       jmp       qword ptr [75C476E8F060]
; Total bytes of code 98
```
```assembly
; System.RuntimeMethodHandle.GetMethodInstantiationPublic(System.IRuntimeMethodInfo)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0B0]
       xor       eax,eax
       mov       [rbp-30],rax
       mov       rbx,rdi
       mov       [rbp-90],rbx
       mov       rdi,rbx
       call      qword ptr [75C474C00320]; Precode of System.RuntimeMethodHandle.EnsureNonNullMethodInfo(System.IRuntimeMethodInfo)
       mov       rdi,[System.Reflection.CustomAttributeExtensions.GetCustomAttributes[[System.__Canon, System.Private.CoreLib]](System.Reflection.MemberInfo, Boolean)]
       cmp       rdi,[rax]
       jne       short M21_L01
       mov       rdi,[rax+50]
M21_L00:
       mov       [rbp-9C],rdi
       lea       rsi,[rbp-30]
       mov       [rbp-0A4],rsi
       xor       edx,edx
       mov       [rbp-94],edx
       lea       rdi,[rbp-88]
       call      qword ptr [75C474BEEF00]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttributes[[System.__Canon, System.Private.CoreLib]](System.Reflection.MemberInfo, Boolean)]
       mov       rdi,[rbp-9C]
       mov       rsi,[rbp-0A4]
       mov       edx,[rbp-94]
       call      qword ptr [rax]
       lea       rdi,[rbp-88]
       call      qword ptr [75C474BEEF08]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,[rbp-30]
       add       rsp,88
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M21_L01:
       mov       rdi,rax
       lea       r11,[System.Reflection.CustomAttributeExtensions.GetCustomAttributes[[System.__Canon, System.Private.CoreLib]](System.Reflection.MemberInfo, Boolean)]
       call      qword ptr [r11]
       mov       rdi,rax
       jmp       short M21_L00
; Total bytes of code 187
```
```assembly
; System.RuntimeType.IsAssignableFrom(System.Type)
       push      rbp
       push      r15
       push      r14
       push      rbx
       push      rax
       lea       rbp,[rsp+20]
       mov       r15,rdi
       mov       rbx,rsi
       test      rbx,rbx
       je        near ptr M22_L12
       cmp       rbx,r15
       je        near ptr M22_L10
       mov       rdi,offset MT_System.RuntimeType
       cmp       [rbx],rdi
       jne       near ptr M22_L13
       mov       r14,rbx
M22_L00:
       test      r14,r14
       je        near ptr M22_L08
       mov       rdi,offset MT_System.RuntimeType
       cmp       [r14],rdi
       jne       near ptr M22_L08
       mov       rdi,[r14+18]
       mov       rsi,rdi
       mov       rdx,[r15+18]
       mov       rax,rdx
       cmp       rsi,rax
       je        near ptr M22_L14
       test      dil,2
       jne       short M22_L01
       test      dl,2
       jne       near ptr M22_L15
M22_L01:
       mov       rdi,75BE85000038
       mov       rdi,[rdi]
       mov       rdx,rsi
       mov       rcx,rax
       add       rdi,10
       rol       rdx,20
       xor       rdx,rcx
       mov       rcx,9E3779B97F4A7C15
       imul      rdx,rcx
       mov       ecx,[rdi]
       shrx      rdx,rdx,rcx
       xor       ecx,ecx
M22_L02:
       lea       r8d,[rdx+1]
       movsxd    r8,r8d
       lea       r8,[r8+r8*2]
       lea       r8,[rdi+r8*8]
       mov       r9d,[r8]
       mov       r10,[r8+8]
       and       r9d,0FFFFFFFE
       cmp       r10,rsi
       jne       short M22_L05
       mov       r10,rax
       xor       r10,[r8+10]
       cmp       r10,1
       ja        short M22_L05
       cmp       r9d,[r8]
       jne       near ptr M22_L16
       mov       edi,r10d
M22_L03:
       test      edi,edi
       jne       short M22_L06
       xor       ebx,ebx
M22_L04:
       movzx     eax,bl
       add       rsp,8
       pop       rbx
       pop       r14
       pop       r15
       pop       rbp
       ret
M22_L05:
       test      r9d,r9d
       je        near ptr M22_L16
       inc       ecx
       add       edx,ecx
       and       edx,[rdi+4]
       cmp       ecx,8
       jl        short M22_L02
       jmp       near ptr M22_L16
M22_L06:
       cmp       edi,1
       je        short M22_L07
       mov       rdi,rsi
       mov       rsi,rax
       mov       edx,1
       call      qword ptr [75C4754349C0]; System.Runtime.CompilerServices.TypeHandle.CanCastToWorker(System.Runtime.CompilerServices.TypeHandle, System.Runtime.CompilerServices.TypeHandle, Boolean)
       mov       ebx,eax
       jmp       short M22_L04
M22_L07:
       mov       ebx,1
       jmp       short M22_L04
M22_L08:
       mov       rsi,rbx
       mov       rdi,offset MT_System.Reflection.Emit.TypeBuilder
       call      System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       je        near ptr M22_L12
       mov       rdi,rbx
       mov       rsi,r15
       mov       rax,[rbx]
       mov       rax,[rax+0B0]
       call      qword ptr [rax+18]
       test      eax,eax
       jne       short M22_L10
       mov       rdi,r15
       call      qword ptr [75C47543CDC8]; System.RuntimeType.get_IsActualInterface()
       test      eax,eax
       jne       short M22_L11
       cmp       [r15],r15b
       mov       rdi,r15
       call      000075C4F34FE670
       test      eax,eax
       je        short M22_L12
       mov       rdi,r15
       call      qword ptr [75C47405A308]
       mov       r15d,[rax+8]
       test      r15d,r15d
       jle       short M22_L10
       lea       r14,[rax+10]
M22_L09:
       mov       rdi,[r14]
       mov       rsi,rbx
       mov       rax,[rdi]
       mov       rax,[rax+0B0]
       call      qword ptr [rax+20]
       test      eax,eax
       je        short M22_L12
       add       r14,8
       dec       r15d
       jne       short M22_L09
M22_L10:
       mov       eax,1
       add       rsp,8
       pop       rbx
       pop       r14
       pop       r15
       pop       rbp
       ret
M22_L11:
       mov       rdi,rbx
       mov       rsi,r15
       add       rsp,8
       pop       rbx
       pop       r14
       pop       r15
       pop       rbp
       jmp       qword ptr [75C476E8F828]
M22_L12:
       xor       eax,eax
       add       rsp,8
       pop       rbx
       pop       r14
       pop       r15
       pop       rbp
       ret
M22_L13:
       mov       rdi,rbx
       mov       rax,[rbx]
       mov       rax,[rax+58]
       call      qword ptr [rax]
       mov       r14,rax
       jmp       near ptr M22_L00
M22_L14:
       mov       edi,1
       jmp       near ptr M22_L03
M22_L15:
       xor       edi,edi
       jmp       near ptr M22_L03
M22_L16:
       mov       edi,2
       jmp       near ptr M22_L03
; Total bytes of code 542
```

## .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3 (Job: MediumRun(IterationCount=15, LaunchCount=2, WarmupCount=10))

```assembly
; NextORM.Benchmark.SqliteBenchmarkStageA.Where_CachedHit_PlanOnly()
       push      rbp
       sub       rsp,70
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-58],rax
       vxorps    xmm8,xmm8,xmm8
       vmovdqu   ymmword ptr [rbp-50],ymm8
       mov       [rbp-30],rdi
       mov       dword ptr [rbp-68],3E8
       xor       eax,eax
       mov       [rbp-38],rax
       xor       eax,eax
       mov       [rbp-3C],eax
       jmp       near ptr M00_L01
M00_L00:
       mov       rdi,7AB93F47FD58
       call      CORINFO_HELP_COUNTPROFILE32
       mov       rax,[rbp-30]
       mov       rax,[rax+8]
       mov       [rbp-48],rax
       mov       rdi,[rbp-30]
       mov       esi,[rbp-3C]
       call      qword ptr [7AB93DA1EDA8]; NextORM.Benchmark.SqliteBenchmarkStageA.BuildWhere(Int32)
       mov       [rbp-50],rax
       call      qword ptr [7AB93F465E30]; NextORM.Benchmark.SqliteBenchmarkStageA.get_Ct()
       mov       [rbp-58],rax
       mov       rdi,[rbp-48]
       mov       rsi,offset MT_NextORM.Core.IQueryPlanner
       mov       rdx,7AB93DF33FE8
       call      qword ptr [7AB93D625908]; System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointer(System.Object, IntPtr, IntPtr)
       mov       [rbp-60],rax
       mov       rdi,[rbp-48]
       mov       rsi,[rbp-50]
       mov       r8,[rbp-58]
       xor       edx,edx
       mov       ecx,1
       mov       rax,[rbp-60]
       call      rax
       mov       [rbp-38],rax
       mov       eax,[rbp-3C]
       inc       eax
       mov       [rbp-3C],eax
M00_L01:
       mov       eax,[rbp-68]
       dec       eax
       mov       [rbp-68],eax
       cmp       dword ptr [rbp-68],0
       jg        short M00_L02
       lea       rdi,[rbp-68]
       mov       esi,24
       call      CORINFO_HELP_PATCHPOINT
M00_L02:
       cmp       dword ptr [rbp-3C],64
       jl        near ptr M00_L00
       mov       rdi,7AB93F47FD5C
       call      CORINFO_HELP_COUNTPROFILE32
       mov       rax,[rbp-38]
       add       rsp,70
       pop       rbp
       ret
; Total bytes of code 241
```
```assembly
; NextORM.Benchmark.SqliteBenchmarkStageA.BuildWhere(Int32)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      rbx
       sub       rsp,40
       vzeroupper
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       [rbp-30],rax
       mov       rbx,rdi
       mov       r15d,esi
       mov       rdi,offset MT_NextORM.Benchmark.SqliteBenchmarkStageA+<>c__DisplayClass8_0
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       [rbp-38],r14
       mov       [r14+8],r15d
       mov       rdi,[rbx+10]
       mov       rbx,[rdi+10]
       mov       [rbp-40],rbx
       mov       r15,7AB938A02190
       mov       [rbp-60],r15
       mov       [rbp-50],r15
       mov       rdi,r15
       mov       rsi,7AB938A027E8
       mov       edx,0FFFFFFFF
       call      qword ptr [7AB93E65E070]; System.Dynamic.Utils.TypeUtils.ValidateType(System.Type, System.String, Int32)
       test      eax,eax
       je        short M01_L00
       mov       rdi,7AB938A02190
       mov       rax,[7AB93C63A0E0]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M01_L12
M01_L00:
       mov       rdi,offset MT_NextORM.Benchmark.SimpleEntity
       mov       rax,7AB9BBA74CD0
       call      rax
       movzx     eax,al
       mov       r15,[rbp-60]
       cmp       eax,10
       sete      bl
       movzx     ebx,bl
       cmp       dword ptr [7AB9BBCCEC80],0
       jne       near ptr M01_L09
M01_L01:
       test      ebx,ebx
       mov       r13,[rbp-50]
       jne       near ptr M01_L10
M01_L02:
       mov       rdi,r13
       mov       edx,ebx
       mov       rsi,7AB938A021B8
       call      qword ptr [7AB93E65E0B8]; System.Linq.Expressions.ParameterExpression.Make(System.Type, System.String, Boolean)
       mov       rbx,rax
       mov       rdi,7AB93DABEDB8
       call      System.RuntimeMethodInfoStub.FromPtr(IntPtr)
       mov       rdi,rax
       call      qword ptr [7AB93DA1EC40]; System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)
       mov       rsi,rax
       test      rsi,rsi
       je        short M01_L03
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [rsi],rdi
       jne       near ptr M01_L11
M01_L03:
       mov       rdi,rbx
       call      qword ptr [7AB93DA1EC58]; System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.MethodInfo)
       mov       r14,rax
       mov       rdi,[rbp-38]
       mov       rsi,7AB938A0A2B8
       call      qword ptr [7AB93DA1EC70]; System.Linq.Expressions.Expression.Constant(System.Object, System.Type)
       mov       r13,rax
       mov       rdi,7AB93ED7F3E8
       call      qword ptr [7AB93ED663A0]; System.RuntimeFieldInfoStub.FromPtr(IntPtr)
       mov       rdi,rax
       call      qword ptr [7AB93ED663B8]; System.Reflection.FieldInfo.GetFieldFromHandle(System.RuntimeFieldHandle)
       mov       rsi,rax
       mov       rdi,r13
       call      qword ptr [7AB93ED663D0]; System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression, System.Reflection.FieldInfo)
       mov       r13,rax
       mov       rdi,r14
       mov       rsi,7AB938A066A0
       mov       edx,0FFFFFFFF
       call      qword ptr [7AB93E65E430]; System.Dynamic.Utils.ExpressionUtils.RequiresCanRead(System.Linq.Expressions.Expression, System.String, Int32)
       mov       rdi,r13
       mov       rsi,7AB938A066C0
       mov       edx,0FFFFFFFF
       call      qword ptr [7AB93E65E430]; System.Dynamic.Utils.ExpressionUtils.RequiresCanRead(System.Linq.Expressions.Expression, System.String, Int32)
       mov       rdx,r14
       mov       rcx,r13
       mov       edi,0D
       mov       rsi,7AB938A06670
       xor       r8d,r8d
       call      qword ptr [7AB93E65E8E0]; System.Linq.Expressions.Expression.GetEqualityComparisonOperator(System.Linq.Expressions.ExpressionType, System.String, System.Linq.Expressions.Expression, System.Linq.Expressions.Expression, Boolean)
       mov       r14,rax
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       mov       esi,1
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       r13,rax
       lea       rdi,[r13+10]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       mov       [rbp-28],r14
       mov       rsi,r13
       mov       rdi,7AB93E884548
       call      qword ptr [7AB93E65E508]; System.Dynamic.Utils.CollectionExtensions.ToReadOnly[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
       mov       rbx,rax
       lea       rsi,[rbp-28]
       mov       rdx,rbx
       mov       rdi,7AB938A06708
       mov       rcx,7AB938A066E0
       call      qword ptr [7AB93E65EB08]; System.Linq.Expressions.Expression.ValidateLambdaArgs(System.Type, System.Linq.Expressions.Expression ByRef, System.Collections.ObjectModel.ReadOnlyCollection`1<System.Linq.Expressions.ParameterExpression>, System.String)
       mov       r8,rbx
       mov       rsi,[rbp-28]
       mov       rdi,offset MT_System.Linq.Expressions.Expression<System.Func<NextORM.Benchmark.SimpleEntity, System.Boolean>>
       xor       edx,edx
       xor       ecx,ecx
       call      qword ptr [7AB93E65EC10]; System.Linq.Expressions.Expression`1[[System.__Canon, System.Private.CoreLib]].Create(System.Linq.Expressions.Expression, System.String, Boolean, System.Collections.Generic.IReadOnlyList`1<System.Linq.Expressions.ParameterExpression>)
       mov       rsi,rax
       mov       rdi,[rbp-40]
       cmp       [rdi],edi
       call      qword ptr [7AB93DA1ECB8]; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].Where(System.Linq.Expressions.Expression`1<System.Func`2<System.__Canon,Boolean>>)
       mov       [rbp-48],rax
       mov       [rbp-58],r15
       mov       rdi,r15
       mov       rsi,7AB938A027E8
       mov       edx,0FFFFFFFF
       call      qword ptr [7AB93E65E070]; System.Dynamic.Utils.TypeUtils.ValidateType(System.Type, System.String, Int32)
       test      eax,eax
       je        short M01_L04
       mov       rdi,7AB938A02190
       mov       rax,[7AB93C63A0E0]
       call      qword ptr [rax+20]
       test      eax,eax
       jne       near ptr M01_L12
M01_L04:
       mov       rdi,offset MT_NextORM.Benchmark.SimpleEntity
       mov       rax,7AB9BBA74CD0
       call      rax
       movzx     eax,al
       cmp       eax,10
       sete      bl
       movzx     ebx,bl
       cmp       dword ptr [7AB9BBCCEC80],0
       jne       near ptr M01_L13
M01_L05:
       test      ebx,ebx
       mov       r14,[rbp-58]
       jne       near ptr M01_L14
M01_L06:
       mov       rdi,r14
       mov       edx,ebx
       mov       rsi,7AB938A021B8
       call      qword ptr [7AB93E65E0B8]; System.Linq.Expressions.ParameterExpression.Make(System.Type, System.String, Boolean)
       mov       rbx,rax
       mov       rdi,7AB93DABEDB8
       call      System.RuntimeMethodInfoStub.FromPtr(IntPtr)
       mov       rdi,rax
       call      qword ptr [7AB93DA1EC40]; System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)
       mov       rsi,rax
       test      rsi,rsi
       je        short M01_L07
       mov       rdi,offset MT_System.Reflection.RuntimeMethodInfo
       cmp       [rsi],rdi
       jne       near ptr M01_L15
M01_L07:
       mov       rdi,rbx
       call      qword ptr [7AB93DA1EC58]; System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.MethodInfo)
       mov       r15,rax
       mov       rdi,offset MT_System.Linq.Expressions.ParameterExpression[]
       mov       esi,1
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       r14,rax
       lea       rdi,[r14+10]
       mov       rsi,rbx
       call      CORINFO_HELP_ASSIGN_REF
       mov       [rbp-30],r15
       mov       rsi,r14
       mov       rdi,7AB93E884548
       call      qword ptr [7AB93E65E508]; System.Dynamic.Utils.CollectionExtensions.ToReadOnly[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>)
       mov       rbx,rax
       lea       rsi,[rbp-30]
       mov       rdx,rbx
       mov       rdi,7AB938A068A0
       mov       rcx,7AB938A066E0
       call      qword ptr [7AB93E65EB08]; System.Linq.Expressions.Expression.ValidateLambdaArgs(System.Type, System.Linq.Expressions.Expression ByRef, System.Collections.ObjectModel.ReadOnlyCollection`1<System.Linq.Expressions.ParameterExpression>, System.String)
       mov       r8,rbx
       mov       rsi,[rbp-30]
       mov       rdi,offset MT_System.Linq.Expressions.Expression<System.Func<NextORM.Benchmark.SimpleEntity, System.Int32>>
       xor       edx,edx
       xor       ecx,ecx
       call      qword ptr [7AB93E65EC10]; System.Linq.Expressions.Expression`1[[System.__Canon, System.Private.CoreLib]].Create(System.Linq.Expressions.Expression, System.String, Boolean, System.Collections.Generic.IReadOnlyList`1<System.Linq.Expressions.ParameterExpression>)
       mov       rbx,rax
       mov       r15,[rbp-48]
       mov       rdi,[r15+0A0]
       test      rdi,rdi
       jne       near ptr M01_L16
M01_L08:
       mov       rdi,r15
       mov       rsi,7AB938A068F0
       call      qword ptr [7AB93E65F9A8]; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].EnsureNoEagerLoadState(System.String)
       mov       rdi,r15
       mov       rdx,rbx
       mov       rsi,7AB93E8A74C8
       call      qword ptr [7AB93E65F948]; NextORM.Core.EntityBuilder`1[[System.__Canon, System.Private.CoreLib]].SelectCore[[System.Int32, System.Private.CoreLib]](System.Linq.Expressions.Expression`1<System.Func`2<System.__Canon,Int32>>)
       nop
       add       rsp,40
       pop       rbx
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M01_L09:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M01_L01
M01_L10:
       mov       rdi,7AB938A02190
       mov       rax,[7AB93C63A0F0]
       call      qword ptr [rax+8]
       mov       r13,rax
       jmp       near ptr M01_L02
M01_L11:
       mov       rsi,rax
       mov       rdi,offset MT_System.Reflection.MethodInfo
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       rsi,rax
       jmp       near ptr M01_L03
M01_L12:
       mov       edi,2ECB
       mov       rsi,7AB93DAB67D0
       call      qword ptr [7AB93D62EF88]
       mov       rdi,rax
       call      qword ptr [7AB93F46F9C0]
       mov       rdi,rax
       call      CORINFO_HELP_THROW
       int       3
M01_L13:
       call      CORINFO_HELP_POLL_GC
       jmp       near ptr M01_L05
M01_L14:
       mov       rdi,7AB938A02190
       mov       rax,[7AB93C63A0F0]
       call      qword ptr [rax+8]
       mov       r14,rax
       jmp       near ptr M01_L06
M01_L15:
       mov       rsi,rax
       mov       rdi,offset MT_System.Reflection.MethodInfo
       call      System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       mov       rsi,rax
       jmp       near ptr M01_L07
M01_L16:
       cmp       dword ptr [rdi+10],0
       jle       near ptr M01_L08
       mov       rdi,offset MT_System.NotSupportedException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       edi,3C76
       mov       rsi,7AB93DAB52D8
       call      qword ptr [7AB93D62EF88]
       mov       r15,rax
       mov       edi,2842
       mov       rsi,7AB93DAB52D8
       call      qword ptr [7AB93D62EF88]
       mov       r14,rax
       mov       edi,3CBA
       mov       rsi,7AB93DAB52D8
       call      qword ptr [7AB93D62EF88]
       mov       rdx,rax
       mov       rdi,r15
       mov       rsi,r14
       call      qword ptr [7AB93DA1F798]; System.String.Concat(System.String, System.String, System.String)
       mov       rsi,rax
       mov       rdi,rbx
       call      qword ptr [7AB93D62EFB8]
       mov       rdi,rbx
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 1287
```
```assembly
; NextORM.Benchmark.SqliteBenchmarkStageA.get_Ct()
       xor       eax,eax
       ret
; Total bytes of code 3
```
```assembly
; System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointer(System.Object, IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rbx
       lea       rbp,[rsp+28]
       mov       rax,[rdi]
       mov       ecx,esi
       rol       ecx,5
       add       ecx,eax
       mov       r8d,edx
       ror       r8d,5
       add       ecx,r8d
       mov       r8,7AB34D000430
       mov       r8,[r8]
       mov       r8,[r8+8]
       movsxd    r9,ecx
       mov       r10,9E3779B97F4A7C15
       imul      r9,r10
       movzx     r10d,byte ptr [r8+10]
       shrx      r9,r9,r10
       xor       r10d,r10d
       jmp       short M03_L01
M03_L00:
       test      ebx,ebx
       je        short M03_L02
       inc       r10d
       add       r9d,r10d
       mov       r11d,[r8+8]
       add       r11d,0FFFFFFFE
       and       r9d,r11d
       cmp       r10d,8
       jge       short M03_L02
M03_L01:
       lea       r11d,[r9+1]
       movsxd    r11,r11d
       imul      r11,30
       lea       r11,[r8+r11+10]
       mov       ebx,[r11]
       mov       r15d,[r11+8]
       mov       r14,[r11+10]
       mov       r13,[r11+18]
       mov       r12,[r11+20]
       cmp       ecx,r15d
       jne       short M03_L00
       mov       r15,rax
       sub       r15,r14
       mov       r14,rsi
       sub       r14,r13
       or        r15,r14
       mov       r14,rdx
       sub       r14,r12
       or        r15,r14
       jne       short M03_L00
       mov       rax,[r11+28]
       and       ebx,0FFFFFFFE
       cmp       ebx,[r11]
       jne       short M03_L02
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M03_L02:
       pop       rbx
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       jmp       qword ptr [7AB93DA1FF18]; System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointerSlow(System.Object, IntPtr, IntPtr)
; Total bytes of code 216
```

