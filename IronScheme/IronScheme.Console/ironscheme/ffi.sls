﻿#| ****************************************************************************
 * Copyright (c) Llewellyn Pritchard. 2007,2008,2009
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. 
 * A copy of the license can be found in the License.html file at the root of this distribution. 
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the 
 * Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 * ***************************************************************************|#

(library (ironscheme ffi)
  (export
    make-pointer-setter
    make-pointer-getter
    make-ffi-callout
    make-ffi-callback
    ffi-callout
    ffi-callback
    pinvoke-call
    pointer?
    pointer=?
    pointer+
    null-pointer
    null-pointer?
    write-int8!
    write-int16!
    write-int32!
    write-int64!
    write-intptr!
    read-int8
    read-int16
    read-int32
    read-int64
    read-intptr
  )
  (import 
    (ironscheme)
    (ironscheme contracts)
    (ironscheme clr)
    (ironscheme clr internal))
    
  (clr-using System.Runtime.InteropServices)
  
  (define-syntax ffi-callout
    (lambda (x)
      (define (->string id) 
        (symbol->string (syntax->datum id)))
      (syntax-case x ()
        [(_ ret (args ...))
          (with-syntax (((args ...) (map ->string #'(ret args ...))))
            #'(ffi-callout-internal args ...))])))
            
  (define-syntax ffi-callback
    (lambda (x)
      (define (->string id) 
        (symbol->string (syntax->datum id)))
      (syntax-case x ()
        [(_ ret (args ...))
          (with-syntax (((args ...) (map ->string #'(ret args ...))))
            #'(ffi-callback-internal args ...))])))    

  (define/contract (write-int8! ptr:pointer ofs:fixnum val:fixnum) 
    (clr-static-call Marshal "WriteByte(IntPtr,Int32,Byte)" ptr ofs val))

  (define/contract (write-int16! ptr:pointer ofs:fixnum val:fixnum) 
    (clr-static-call Marshal "WriteInt16(IntPtr,Int32,Int16)" ptr ofs val))

  (define/contract (write-int32! ptr:pointer ofs:fixnum val:fixnum) 
    (clr-static-call Marshal "WriteInt32(IntPtr,Int32,Int32)" ptr ofs val))

  (define/contract (write-int64! ptr:pointer ofs:fixnum val:integer) 
    (clr-static-call Marshal "WriteInt64(IntPtr,Int32,Int64)" ptr ofs val))

  (define/contract (write-intptr! ptr:pointer ofs:fixnum val:pointer) 
    (clr-static-call Marshal "WriteIntPtr(IntPtr,Int32,IntPtr)" ptr ofs val))
    
  (define/contract (read-int8 ptr:pointer ofs:fixnum) 
    (clr-static-call Marshal "ReadByte(IntPtr,Int32)" ptr ofs))

  (define/contract (read-int16 ptr:pointer ofs:fixnum) 
    (clr-static-call Marshal "ReadInt16(IntPtr,Int32)" ptr ofs))

  (define/contract (read-int32 ptr:pointer ofs:fixnum) 
    (clr-static-call Marshal "ReadInt32(IntPtr,Int32)" ptr ofs))

  (define/contract (read-int64 ptr:pointer ofs:fixnum) 
    (clr-static-call Marshal "ReadInt64(IntPtr,Int32)" ptr ofs))

  (define/contract (read-intptr ptr:pointer ofs:fixnum) 
    (clr-static-call Marshal "ReadIntPtr(IntPtr,Int32)" ptr ofs))
    
  (define/contract (make-pointer-getter sym:symbol)
    (case sym
      [(int8 uint8)  read-int8]
      [(int16 uint16)  read-int16]
      [(int32 uint32)  read-int32]
      [(int64 uint64)  read-int64]
      [(intptr uintptr) read-intptr]
      [else (assertion-violation 'make-pointer-getter "unknown type" sym)]))
      
  (define/contract (make-pointer-setter sym:symbol)
    (case sym
      [(int8 uint8)  write-int8!]
      [(int16 uint16)  write-int16!]
      [(int32 uint32)  write-int32!]
      [(int64 uint64)  write-int64!]
      [(intptr uintptr) write-intptr!]
      [else (assertion-violation 'make-pointer-setter "unknown type" sym)]))      
      
  (define (make-ffi-callout return-type arg-types)
    (eval `(ffi-callout ,return-type ,arg-types) 
           (environment '(ironscheme clr))))      

  (define (make-ffi-callback return-type arg-types)
    (eval `(ffi-callback ,return-type ,arg-types) 
           (environment '(ironscheme clr))))  
           
  (define (pointer? obj)
    (clr-is System.IntPtr obj))
    
  (define/contract (pointer=? p1:pointer p2:pointer)
    (clr-static-call System.IntPtr op_Equality p1 p2))
    
  (define (null-pointer)
    (clr-static-field-get System.IntPtr Zero))
    
  (define/contract (null-pointer? obj:pointer)
    (clr-static-call System.IntPtr op_Equality (null-pointer) obj))
)    
  